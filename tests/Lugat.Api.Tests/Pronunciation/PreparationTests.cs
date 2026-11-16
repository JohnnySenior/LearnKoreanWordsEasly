using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lugat.Api.Tests.Ai;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Api.Tests.Teaching;
using Lugat.Contracts;
using Lugat.Domain.Ai;
using Lugat.Domain.Catalog;
using Lugat.Domain.Users;
using Lugat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lugat.Api.Tests.Pronunciation;

public class PreparationTests
{
    private static readonly (string Korean, string Uzbek)[] Words = [("국물", "sho‘rva suvi"), .. ApiFactory.Words("가", 24)];
    private static string Answer(Lugat.Application.Ai.AiJsonRequest request, bool uncertain = false)
    {
        using var data = JsonDocument.Parse(request.UserContent);
        return JsonSerializer.Serialize(new { words = data.RootElement.EnumerateArray().Select(w => new {
            id = w.GetProperty("id").GetGuid(), korean = w.GetProperty("korean").GetString(),
            reading = uncertain && w.GetProperty("korean").GetString() == "국물" ? null : w.GetProperty("korean").GetString() == "국물" ? "궁물" : w.GetProperty("korean").GetString(),
            explanationUz = uncertain || w.GetProperty("korean").GetString() != "국물" ? null : "ㄱ tovushi ㅁ oldida ㅇ kabi aytiladi.",
        }).ToArray() });
    }
    private static ApiFactory Factory(FakeAiProvider ai) => new() { Premium = false, AdminTelegramIds = [ApiFactory.UserA], TableWords = Words, AiProviders = [ai] };
    private static async Task<T> Ok<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(ApiFactory.Json))!;
    }
    private static Task<UnitAdminDto?> Unit(HttpClient c) => c.GetFromJsonAsync<UnitAdminDto>($"/api/admin/units/{ApiFactory.Unit(1)}", ApiFactory.Json);

    [Fact]
    public async Task All_25_published_words_prepare_in_three_calls_and_approve_in_one_request_without_resetting_progress()
    {
        var ai = new FakeAiProvider(AiProviderKind.OpenAi) { Answer = r => Answer(r) };
        await using var f = Factory(ai);
        var editor = await f.SignedInClientAsync(ApiFactory.UserA);
        var learner = await f.SignedInClientAsync(ApiFactory.UserB);
        await learner.PostAsync(ApiFactory.TestWordsPath, null);
        List<Guid> cards;
        await using (var scope = f.Services.CreateAsyncScope()) cards = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Cards.Select(c => c.Id).OrderBy(id => id).ToListAsync();
        UnitPreparationDto? step = null;
        for (var i = 0; i < 3; i++)
            step = await Ok<UnitPreparationDto>(await editor.PostAsync($"/api/admin/units/{ApiFactory.Unit(1)}/prepare", null));
        Assert.True(step!.Done);
        Assert.Equal(3, ai.Requests.Count);
        Assert.All(ai.Requests, r => Assert.Equal("pronunciation_list", r.SchemaName));
        Assert.All(step.Unit.Words, w => { Assert.False(w.PronunciationReview!.Reviewed); Assert.NotNull(w.PronunciationReview.Reading); });
        Assert.Equal(0, (await learner.GetFromJsonAsync<UnitDto>($"/api/units/{ApiFactory.Unit(1)}", ApiFactory.Json))!.PronunciationCount);
        await Ok<UnitPreparationDto>(await editor.PostAsync($"/api/admin/units/{ApiFactory.Unit(1)}/prepare", null));
        Assert.Equal(3, ai.Requests.Count);
        var approved = await Ok<UnitAdminDto>(await editor.PutAsJsonAsync($"/api/admin/units/{ApiFactory.Unit(1)}/pronunciations/review",
            new ReviewPronunciationsRequest(step.Unit.Words.Select(w => new PronunciationWordReviewInput(w.Id, w.PronunciationReview!.Reading, w.PronunciationReview.ExplanationUz, w.Version)).ToList()), ApiFactory.Json));
        Assert.All(approved.Words, w => Assert.True(w.PronunciationReview!.Reviewed));
        Assert.Equal(UnitStatus.Published, approved.Unit.Status);
        Assert.Equal(1, (await learner.GetFromJsonAsync<UnitDto>($"/api/units/{ApiFactory.Unit(1)}", ApiFactory.Json))!.PronunciationCount);
        await using var check = f.Services.CreateAsyncScope();
        Assert.Equal(cards, await check.ServiceProvider.GetRequiredService<AppDbContext>().Cards.Select(c => c.Id).OrderBy(id => id).ToListAsync());
    }

    [Fact]
    public async Task An_uncertain_word_is_not_recharged_or_approved_and_stale_batch_approval_is_atomic()
    {
        var ai = new FakeAiProvider(AiProviderKind.OpenAi) { Answer = r => Answer(r, true) };
        await using var f = Factory(ai);
        var editor = await f.SignedInClientAsync(ApiFactory.UserA);
        await editor.PostAsync(ApiFactory.TestWordsPath, null);
        var url = $"/api/admin/units/{ApiFactory.Unit(1)}/pronunciations";
        var unit = await Ok<UnitAdminDto>(await editor.PostAsync(url + "/draft", null));
        var unknown = unit.Words.Single(w => w.Word == "국물");
        Assert.Null(unknown.PronunciationReview!.Reading);
        Assert.NotNull(unknown.PronunciationReview.AskedAt);
        await editor.PostAsync(url + "/draft", null);
        await editor.PostAsync(url + "/draft", null);
        await editor.PostAsync(url + "/draft", null);
        Assert.Equal(3, ai.Requests.Count);
        var chosen = unit.Words.Where(w => w.PronunciationReview?.Reading is not null).Take(2).ToList();
        var changed = chosen[0];
        await Ok<PronunciationReviewDto>(await editor.PutAsJsonAsync($"/api/admin/catalog/words/{changed.Id}/pronunciation",
            new ReviewPronunciationRequest(changed.Word, null, false, changed.Version), ApiFactory.Json));
        // The single-word save is a no-op; use a different explanation so the row version changes.
        var fresh = (await Unit(editor))!.Words.Single(w => w.Id == changed.Id);
        await editor.PutAsJsonAsync($"/api/admin/catalog/words/{changed.Id}/pronunciation", new ReviewPronunciationRequest(changed.Word, "Tekshiruv izohi.", false, fresh.Version), ApiFactory.Json);
        var response = await editor.PutAsJsonAsync(url + "/review", new ReviewPronunciationsRequest(chosen.Select(w => new PronunciationWordReviewInput(w.Id, w.PronunciationReview!.Reading, null, w.Version)).ToList()), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        unit = (await Unit(editor))!;
        Assert.All(unit.Words, w => Assert.False(w.PronunciationReview!.Reviewed));
    }

    [Fact]
    public async Task Preparation_does_not_replace_existing_approved_readings_and_learners_cannot_trigger_it()
    {
        var ai = new FakeAiProvider(AiProviderKind.OpenAi) { Answer = r => Answer(r) };
        await using var f = Factory(ai);
        var editor = await f.SignedInClientAsync(ApiFactory.UserA);
        var learner = await f.SignedInClientAsync(ApiFactory.UserB);
        await learner.PostAsync(ApiFactory.TestWordsPath, null);
        var word = (await Unit(editor))!.Words.Single(w => w.Word == "국물");
        await editor.PutAsJsonAsync($"/api/admin/catalog/words/{word.Id}/pronunciation", new ReviewPronunciationRequest("궁물", "Muharrir izohi.", true, word.Version), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Forbidden, (await learner.PostAsync($"/api/admin/units/{ApiFactory.Unit(1)}/prepare", null)).StatusCode);
        Assert.Empty(ai.Requests);
        await editor.PostAsync($"/api/admin/units/{ApiFactory.Unit(1)}/prepare", null);
        Assert.DoesNotContain(word.Id.ToString(), ai.Requests.Single().UserContent);
        var saved = (await Unit(editor))!.Words.Single(w => w.Id == word.Id);
        Assert.Equal("Muharrir izohi.", saved.PronunciationReview!.ExplanationUz);
        Assert.True(saved.PronunciationReview.Reviewed);
    }

    [Fact]
    public async Task Group_batch_respects_ownership_and_paid_AI_but_manual_approval_is_free()
    {
        var ai = new FakeAiProvider(AiProviderKind.OpenAi) { Answer = r => Answer(r) };
        await using var f = Factory(ai);
        var owner = await f.SignedInClientAsync(ApiFactory.UserB);
        await TeachingTests.GrantAsync(f, ApiFactory.UserB, UserRoles.Teacher);
        var group = await Ok<TeacherGroupDto>(await owner.PostAsJsonAsync("/api/teacher/groups", new CreateGroupRequest(Guid.NewGuid(), "Guruh"), ApiFactory.Json));
        var deck = await Ok<GroupDeckDto>(await owner.PostAsJsonAsync($"/api/teacher/groups/{group.Id}/decks", new CreateGroupDeckRequest(Guid.NewGuid(), "Lug‘at"), ApiFactory.Json));
        deck = await Ok<GroupDeckDto>(await owner.PostAsJsonAsync($"/api/teacher/decks/{deck.Id}/words", new AddGroupWordRequest("국물", "sho‘rva suvi"), ApiFactory.Json));
        var url = $"/api/teacher/decks/{deck.Id}/pronunciations";
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsync(url + "/draft", null)).StatusCode);
        Assert.Empty(ai.Requests);
        var other = await f.SignedInClientAsync(ApiFactory.UserA);
        await TeachingTests.GrantAsync(f, ApiFactory.UserA, UserRoles.Teacher);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync(url + "/draft", null)).StatusCode);
        var word = deck.Words.Single();
        deck = await Ok<GroupDeckDto>(await owner.PutAsJsonAsync(url + "/review", new ReviewPronunciationsRequest([new(word.Id, "궁물", "Tovush o‘zgaradi.", word.PronunciationReview!.Version)]), ApiFactory.Json));
        Assert.True(deck.Words.Single().PronunciationReview!.Reviewed);
        var anotherGroup = await Ok<TeacherGroupDto>(await other.PostAsJsonAsync("/api/teacher/groups", new CreateGroupRequest(Guid.NewGuid(), "Boshqa guruh"), ApiFactory.Json));
        await Ok<MyGroupDto>(await owner.PostAsync($"/api/groups/join/{anotherGroup.InviteCode}", null));
        var anotherDeck = await Ok<GroupDeckDto>(await other.PostAsJsonAsync($"/api/teacher/groups/{anotherGroup.Id}/decks", new CreateGroupDeckRequest(Guid.NewGuid(), "Yangi lug‘at"), ApiFactory.Json));
        anotherDeck = await Ok<GroupDeckDto>(await other.PostAsJsonAsync($"/api/teacher/decks/{anotherDeck.Id}/words", new AddGroupWordRequest("국물", "sho‘rva suvi"), ApiFactory.Json));
        anotherDeck = await Ok<GroupDeckDto>(await other.PostAsync($"/api/teacher/decks/{anotherDeck.Id}/pronunciations/draft", null));
        var draft = anotherDeck.Words.Single();
        Assert.False(draft.PronunciationReview!.Reviewed);
        Assert.NotNull(draft.PronunciationReview.AskedAt);
        await Ok<GroupDeckDto>(await other.PostAsync($"/api/teacher/decks/{anotherDeck.Id}/publish", null));
        await Ok<GroupDeckDto>(await other.PutAsJsonAsync($"/api/teacher/decks/{anotherDeck.Id}/pronunciations/review", new ReviewPronunciationsRequest([new(draft.Id, draft.PronunciationReview.Reading, draft.PronunciationReview.ExplanationUz, draft.PronunciationReview.Version)]), ApiFactory.Json));
        var learnerView = await owner.GetFromJsonAsync<GroupDeckLearnerDto>($"/api/my-words/group-decks/{anotherDeck.Id}", ApiFactory.Json);
        Assert.Equal(1, learnerView!.PronunciationCount);
        Assert.Equal("궁물", learnerView.Words.Single().Pronunciation!.Reading);
    }
    [Fact]
    public async Task One_preparation_flow_runs_cards_traps_and_readings_without_publishing_or_reviewing()
    {
        var ai = new FakeAiProvider(AiProviderKind.OpenAi)
        {
            Answer = r => r.SchemaName switch
            {
                "catalog_draft" => """{"words":[{"id":1,"translationUz":"sho‘rva suvi","romanization":"gungmul","exampleKo":"국물이 있어요.","exampleUz":"Sho‘rva suvi bor.","trapUz":"Tovushga e’tibor bering.","theme":"Ovqat","questions":[]}]}""",
                "trap_scenes" => """{"words":[{"id":1,"cue":null,"wrong":null,"right":null}]}""",
                _ => Answer(r),
            },
        };
        await using var f = Factory(ai);
        var editor = await f.SignedInClientAsync(ApiFactory.UserA);
        await editor.PostAsync(ApiFactory.TestWordsPath, null);
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var unit = await db.Units.SingleAsync(u => u.Id == ApiFactory.Unit(1));
            unit.Status = UnitStatus.Draft;
            var word = await db.CatalogWords.SingleAsync(w => w.Word == "국물");
            word.Status = CatalogWordStatus.Draft;
            word.TranslationUz = null;
            word.DraftedAt = null;
            await db.SaveChangesAsync();
        }
        UnitPreparationDto? done = null;
        for (var i = 0; i < 5; i++) done = await Ok<UnitPreparationDto>(await editor.PostAsync($"/api/admin/units/{ApiFactory.Unit(1)}/prepare", null));
        Assert.True(done!.Done);
        Assert.Equal(new[] { "catalog_draft", "trap_scenes", "pronunciation_list", "pronunciation_list", "pronunciation_list" }, ai.Requests.Select(r => r.SchemaName));
        Assert.Equal(UnitStatus.Draft, done.Unit.Unit.Status);
        var first = done.Unit.Words.Single(w => w.Word == "국물");
        Assert.Equal(CatalogWordStatus.Draft, first.Status);
        Assert.False(first.PronunciationReview!.Reviewed);
        Assert.True(first.TrapSceneAsked);
    }

    [Fact]
    public async Task A_failed_batch_resumes_after_saved_words_without_requesting_them_again()
    {
        var count = 0;
        var ai = new FakeAiProvider(AiProviderKind.OpenAi) { Answer = r => ++count == 2 ? "bad JSON" : Answer(r) };
        await using var f = Factory(ai);
        var editor = await f.SignedInClientAsync(ApiFactory.UserA);
        await editor.PostAsync(ApiFactory.TestWordsPath, null);
        var url = $"/api/admin/units/{ApiFactory.Unit(1)}/prepare";
        await Ok<UnitPreparationDto>(await editor.PostAsync(url, null));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await editor.PostAsync(url, null)).StatusCode);
        var saved = (await Unit(editor))!.Words.Where(w => w.PronunciationReview!.Reading is not null).Select(w => w.Id).ToList();
        Assert.Equal(10, saved.Count);
        await Ok<UnitPreparationDto>(await editor.PostAsync(url, null));
        Assert.True((await Ok<UnitPreparationDto>(await editor.PostAsync(url, null))).Done);
        Assert.Equal(4, ai.Requests.Count);
        foreach (var id in saved)
            foreach (var request in ai.Requests.Skip(1)) Assert.DoesNotContain(id.ToString(), request.UserContent);
    }

}
