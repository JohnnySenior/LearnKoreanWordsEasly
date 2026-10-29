using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lugat.Api.Tests.Ai;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Application.Catalog;
using Lugat.Contracts;
using Lugat.Domain.Ai;
using Lugat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lugat.Api.Tests.Catalog;

/// <summary>"Tuzoq sahnasi" (L-52): the trap acted out on the card, drafted by the AI and confirmed by an editor.</summary>
public class TrapSceneTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly FakeAiProvider _ai = new(AiProviderKind.Anthropic);

    private static readonly (string, string)[] Words = [("가다", "bormoq"), ("하다", "qilmoq"), ("먹다", "yemoq")];

    private const string GoTrap = "Yo‘nalish 에 bilan: 회사에 가요 = ishga boraman. 회사에서 가요 deyish xato.";

    private ApiFactory NewFactory() => new() { AdminTelegramIds = [ApiFactory.UserA], AiProviders = [_ai] };

    /// <summary>Unit 1 published with trap notes on 가다 and 하다 (하다's names no mistake); the learner opened it.</summary>
    private static async Task<(HttpClient Admin, HttpClient Learner)> SetUpAsync(ApiFactory factory)
    {
        var admin = await factory.SignedInClientAsync(ApiFactory.UserA);
        var learner = await factory.SignedInClientAsync(ApiFactory.UserB);
        await factory.AddWordsAsync(ApiFactory.UserB, 1, Words);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var w in await db.CatalogWords.Where(w => w.UnitId == ApiFactory.Unit(1)).ToListAsync())
        {
            w.TrapUz = w.Word switch
            {
                "가다" => GoTrap,
                "하다" => "Yaxshi xabar: o‘zbekchadagi \"ot + qilmoq\" kabi ishlaydi: 공부하다.",
                _ => null,
            };
        }

        await db.SaveChangesAsync();
        return (admin, learner);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    /// <summary>The learner's 가다 card, answer side included.</summary>
    private static async Task<CardPromptDto> GoCardAsync(ApiFactory factory, HttpClient learner)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var item = await db.LearningItems.Where(i => i.Korean == "가다").Select(i => i.Id).SingleAsync();
        var start = await learner.PostAsJsonAsync("/api/sessions", new StartSessionRequest(null, true, LearningItemIds: [item]), Json);
        return (await start.Content.ReadFromJsonAsync<StartSessionResponse>(Json))!.Session!.Current!;
    }

    private static string Scenes(string userContent) => userContent.Contains("1. 가다")
        ? """{"words":[{"id":1,"cue":"«Ishga boraman» deyishda:","wrong":"회사에서 가요","right":"회사에 가요"},{"id":2,"cue":null,"wrong":null,"right":null}]}"""
        : """{"words":[]}""";

    [Fact]
    public async Task The_ai_drafts_a_scene_learners_see_it_only_after_an_editor_confirms_and_a_note_without_a_mistake_gets_none()
    {
        _ai.Answer = r => Scenes(r.UserContent);
        await using var factory = NewFactory();
        var (admin, learner) = await SetUpAsync(factory);
        var url = $"/api/admin/units/{ApiFactory.Unit(1)}";

        var before = await ReadAsync<UnitAdminDto>(await admin.GetAsync(url));
        var drafted = await ReadAsync<UnitAdminDto>(await admin.PostAsync(url + "/trap-scenes", null));
        var again = await ReadAsync<UnitAdminDto>(await admin.PostAsync(url + "/trap-scenes", null));
        var pendingCard = await GoCardAsync(factory, learner);
        var forbidden = await learner.PostAsync(url + "/trap-scenes", null);

        Assert.Equal(2, before.ScenesRemaining);
        Assert.Equal(0, drafted.ScenesRemaining);
        Assert.Single(_ai.Requests, r => r.Feature == TrapSceneService.Feature); // nothing left to ask, 하다 included
        Assert.Equal(0, again.ScenesRemaining);
        var go = drafted.Words.Single(w => w.Word == "가다");
        Assert.Equal(("«Ishga boraman» deyishda:", "회사에서 가요", "회사에 가요", true), (go.TrapCue, go.TrapWrong, go.TrapRight, go.TrapScenePending));
        Assert.Null(drafted.Words.Single(w => w.Word == "하다").TrapWrong);
        Assert.Null(pendingCard.TrapScene); // a draft is not shown
        Assert.Equal(GoTrap, pendingCard.UsageTrap);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var approved = await ReadAsync<CatalogWordAdminDto>(await admin.PostAsJsonAsync($"/api/admin/catalog/words/{go.Id}/trap-scene/approve", new ReviewCatalogWordRequest(go.Version), Json));
        var card = await GoCardAsync(factory, learner);

        Assert.False(approved.TrapScenePending);
        Assert.Equal(go.ContentVersion + 1, approved.ContentVersion);
        Assert.Equal(new TrapSceneDto("«Ishga boraman» deyishda:", "회사에서 가요", "회사에 가요"), card.TrapScene);
        Assert.Equal(GoTrap, card.UsageTrap); // the note stays as the explanation
    }

    [Fact]
    public async Task The_review_list_confirms_all_drops_one_and_words_without_a_scene_can_be_asked_again()
    {
        _ai.Answer = r => Scenes(r.UserContent);
        await using var factory = NewFactory();
        var (admin, learner) = await SetUpAsync(factory);
        var url = $"/api/admin/units/{ApiFactory.Unit(1)}";
        var drafted = await ReadAsync<UnitAdminDto>(await admin.PostAsync(url + "/trap-scenes", null));
        var doWord = drafted.Words.Single(w => w.Word == "하다");

        // 하다: asked, nothing found; the page can tell it from a word never asked.
        Assert.True(doWord.TrapSceneAsked);
        Assert.Null(doWord.TrapWrong);
        Assert.False(drafted.Words.Single(w => w.Word == "먹다").TrapSceneAsked); // no trap note

        var all = await ReadAsync<UnitAdminDto>(await admin.PostAsync(url + "/trap-scenes/approve", null));
        var go = all.Words.Single(w => w.Word == "가다");
        Assert.False(go.TrapScenePending);
        Assert.NotNull((await GoCardAsync(factory, learner)).TrapScene);

        var rejected = await ReadAsync<CatalogWordAdminDto>(await admin.PostAsJsonAsync($"/api/admin/catalog/words/{go.Id}/trap-scene/reject", new ReviewCatalogWordRequest(go.Version), Json));
        Assert.Null(rejected.TrapWrong);
        Assert.True(rejected.TrapSceneAsked);
        Assert.Null((await GoCardAsync(factory, learner)).TrapScene);
        Assert.Equal(go.ContentVersion + 1, rejected.ContentVersion);

        var retried = await ReadAsync<UnitAdminDto>(await admin.PostAsync(url + "/trap-scenes/retry", null));
        Assert.Equal(2, retried.ScenesRemaining); // 가다 and 하다 may be asked again
        var again = await ReadAsync<UnitAdminDto>(await admin.PostAsync(url + "/trap-scenes", null));
        Assert.True(again.Words.Single(w => w.Word == "가다").TrapScenePending);
        Assert.Equal(HttpStatusCode.Forbidden, (await learner.PostAsync(url + "/trap-scenes/approve", null)).StatusCode);
    }

    [Fact]
    public async Task An_editor_writes_or_clears_a_scene_by_hand_and_a_changed_note_can_be_asked_again()
    {
        _ai.Answer = r => Scenes(r.UserContent);
        await using var factory = NewFactory();
        var (admin, learner) = await SetUpAsync(factory);
        var unit = await ReadAsync<UnitAdminDto>(await admin.PostAsync($"/api/admin/units/{ApiFactory.Unit(1)}/trap-scenes", null));
        var eat = unit.Words.Single(w => w.Word == "먹다");
        var doWord = unit.Words.Single(w => w.Word == "하다");
        UpdateCatalogWordRequest Edit(CatalogWordAdminDto w, string? trap, string? cue, string? wrong, string? right) =>
            new(w.TranslationUz, w.Romanization ?? "meokda", w.ExampleKo ?? "밥을 먹어요.", w.ExampleUz ?? "Ovqat yeyman.", trap, w.Theme, w.Questions, w.Version, cue, wrong, right);

        var noNote = await admin.PutAsJsonAsync($"/api/admin/catalog/words/{eat.Id}", Edit(eat, null, null, "약을 마셔요", "약을 먹어요"), Json);
        var half = await admin.PutAsJsonAsync($"/api/admin/catalog/words/{eat.Id}", Edit(eat, "Dori yeyiladi: 약을 먹다.", null, "약을 마셔요", null), Json);
        var cyrillic = await admin.PutAsJsonAsync($"/api/admin/catalog/words/{eat.Id}", Edit(eat, "Dori yeyiladi: 약을 먹다.", "Дори ичаман:", "약을 마셔요", "약을 먹어요"), Json);
        var same = await admin.PutAsJsonAsync($"/api/admin/catalog/words/{eat.Id}", Edit(eat, "Dori yeyiladi: 약을 먹다.", null, "약을 먹어요", "약을  먹어요"), Json);
        var saved = await ReadAsync<CatalogWordAdminDto>(await admin.PutAsJsonAsync($"/api/admin/catalog/words/{eat.Id}",
            Edit(eat, "Dori yeyiladi: 약을 먹다.", "«Dori ichdim» deyishda:", "약을 마셨어요", "약을 먹었어요"), Json));
        // A new note on 하다: the AI may be asked again.
        var renoted = await ReadAsync<CatalogWordAdminDto>(await admin.PutAsJsonAsync($"/api/admin/catalog/words/{doWord.Id}",
            Edit(doWord, "Ko‘pchilik 공부를 하다 deydi, qisqasi 공부하다 ham bor.", null, null, null), Json));
        var after = await ReadAsync<UnitAdminDto>(await admin.GetAsync($"/api/admin/units/{ApiFactory.Unit(1)}"));

        foreach (var (bad, why) in new[] { (noNote, "izoh"), (half, "birga"), (cyrillic, "lotin"), (same, "bir xil") })
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, bad.StatusCode);
            Assert.Contains(why, await bad.Content.ReadAsStringAsync()); // refused for the scene, not for another field
        }

        Assert.Equal(("약을 마셨어요", "약을 먹었어요", false), (saved.TrapWrong, saved.TrapRight, saved.TrapScenePending)); // a hand-written scene is confirmed
        Assert.Null(renoted.TrapWrong);
        Assert.Equal(1, after.ScenesRemaining);

        // Clearing both lines leaves the plain note.
        var cleared = await ReadAsync<CatalogWordAdminDto>(await admin.PutAsJsonAsync($"/api/admin/catalog/words/{eat.Id}",
            Edit(saved, saved.TrapUz, null, null, null), Json));
        Assert.Null(cleared.TrapWrong);
    }

    [Fact]
    public async Task An_own_ai_word_keeps_its_scene_on_the_card()
    {
        _ai.Answer = _ => """
            {"words":[{"korean":"약","translationUz":"dori","romanization":"yak","exampleKo":"약을 먹어요.","exampleUz":"Dori ichaman.",
              "trapUz":"O‘zbekchada dori ichiladi, koreyschada yeyiladi: 약을 먹다.","trapCue":"«Dori ichdim» deyishda:","trapWrong":"약을 마셨어요","trapRight":"약을 먹었어요"}]}
            """;
        await using var factory = NewFactory();
        var client = await factory.SignedInClientAsync(ApiFactory.UserB);

        var lookup = await ReadAsync<MyWordLookupDto>(await client.PostAsJsonAsync("/api/my-words/lookup", new MyWordLookupRequest("dori"), Json));
        var c = Assert.Single(lookup.Candidates);
        var add = await client.PostAsJsonAsync("/api/my-words", new AddMyWordRequest(Guid.NewGuid(), c.Korean, c.Uzbek, c.Romanization, c.ExampleKo, c.ExampleUz, c.TrapUz,
            c.Source, null, null, c.TrapScene), Json);
        var word = await add.Content.ReadFromJsonAsync<MyWordDto>(Json);
        var start = await client.PostAsJsonAsync("/api/sessions", new StartSessionRequest(null, true, LearningItemIds: [word!.LearningItemId]), Json);
        var card = (await start.Content.ReadFromJsonAsync<StartSessionResponse>(Json))!.Session!.Current!;

        Assert.Equal(new TrapSceneDto("«Dori ichdim» deyishda:", "약을 마셨어요", "약을 먹었어요"), c.TrapScene);
        Assert.Equal(c.TrapScene, card.TrapScene);
    }

    [Theory]
    [InlineData(null, null, null, "izoh", true, null)]
    [InlineData(null, "회사에서 가요", "회사에 가요", "izoh", true, "회사에 가요")]
    [InlineData("  «Ishga» deyishda:  ", "회사에서 가요", "회사에 가요", "izoh", true, "회사에 가요")]
    [InlineData(null, "회사에서 가요", null, "izoh", false, null)]
    [InlineData(null, "회사에서 가요", "회사에 가요", null, false, null)]
    [InlineData(null, "office", "회사에 가요", "izoh", false, null)]
    [InlineData(null, "회사에 가요", "회사에  가요", "izoh", false, null)]
    public void A_scene_needs_both_korean_lines_a_note_and_two_different_sides(string? cue, string? wrong, string? right, string? note, bool ok, string? expectedRight)
    {
        var (scene, problem) = CatalogContent.CheckScene(cue, wrong, right, note);

        Assert.Equal(ok, problem is null);
        Assert.Equal(expectedRight, scene?.Right);
        if (cue is not null && scene is not null)
        {
            Assert.Equal("«Ishga» deyishda:", scene.Cue);
        }
    }
}
