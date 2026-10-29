using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lugat.Api.Tests.Ai;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Api.Tests.Speech;
using Lugat.Application.Catalog;
using Lugat.Contracts;
using Lugat.Domain.Ai;
using Lugat.Domain.Catalog;
using Lugat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lugat.Api.Tests.Catalog;

public sealed class FakeNarrator : IIntroductionNarrator
{
    public bool IsConfigured => true;
    public string Profile => "test/uz/voice";
    public string Model => "test-uz";
    public List<string> Texts { get; } = [];
    public bool Fail { get; set; }
    public Task<byte[]> SynthesizeAsync(string text, CancellationToken ct)
    {
        Texts.Add(text);
        return Fail ? throw new Lugat.Application.ServiceUnavailableException("Sinov ovoz xatosi.") : Task.FromResult("ID3narration"u8.ToArray());
    }
}

public class IntroductionTests
{
    private static readonly JsonSerializerOptions Json = ApiFactory.Json;
    private static string Root(Guid id) => $"/api/admin/introductions/{id}";
    private static ApiFactory Factory(FakeAiProvider? ai = null, FakeNarrator? narrator = null) => new()
    {
        AdminTelegramIds = [ApiFactory.UserA], AiProviders = ai is null ? [] : [ai], Narrator = narrator ?? new FakeNarrator(), Speech = new FakeSpeech(),
    };
    private static async Task<(HttpClient Admin, HttpClient Learner)> Setup(ApiFactory f)
    {
        var admin = await f.SignedInClientAsync(ApiFactory.UserA);
        var learner = await f.SignedInClientAsync(ApiFactory.UserB);
        await f.AddWordsAsync(ApiFactory.UserB, 1, ("가다", "bormoq"), ("하다", "qilmoq"));
        return (admin, learner);
    }
    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }
    private static async Task<IntroductionDto> Create(HttpClient admin) => await Read<IntroductionDto>(await admin.PostAsJsonAsync(
        $"/api/admin/units/{ApiFactory.Unit(1)}/introductions", new CreateIntroductionRequest(Guid.NewGuid()), Json));
    private static UpdateIntroductionWordRequest Word(uint version, bool reviewed = true) => new(version, "Harakatni aytamiz",
        [new("meaning", "Bu kundalik hayotda kerak bo‘ladigan harakat.", null, "Asosiy harakat"),
         new("example", "Misolga qarang. Harakat oxirida muloyim shakl ishlatiladi.", "집에 가요.", "Uyga boraman.")], reviewed);
    private static async Task<IntroductionDto> Fill(HttpClient admin, IntroductionDto row)
    {
        var unit = (await admin.GetFromJsonAsync<UnitAdminDto>($"/api/admin/units/{row.UnitId}", Json))!;
        foreach (var word in unit.Words) row = await Read<IntroductionDto>(await admin.PutAsJsonAsync(Root(row.Id) + $"/words/{word.Id}", Word(row.Version), Json));
        return row;
    }
    private static async Task<IntroductionDto> Approve(HttpClient admin, IntroductionDto row) => await Read<IntroductionDto>(await admin.PostAsJsonAsync(Root(row.Id) + "/approve", new IntroductionVersionRequest(row.Version), Json));
    private static async Task<IntroductionDto> Audio(HttpClient admin, IntroductionDto row)
    {
        while (row.RemainingAudio > 0) row = await Read<IntroductionDto>(await admin.PostAsJsonAsync(Root(row.Id) + "/audio-next", new IntroductionVersionRequest(row.Version), Json));
        return row;
    }
    private static async Task<IntroductionDto> Publish(HttpClient admin, IntroductionDto row) => await Read<IntroductionDto>(await admin.PostAsJsonAsync(Root(row.Id) + "/publish", new PublishIntroductionRequest(row.Version, true), Json));

    [Fact]
    public async Task Only_published_human_checked_versions_and_their_audio_are_visible_and_replacement_keeps_old_live_until_publish()
    {
        var narrator = new FakeNarrator();
        await using var f = Factory(narrator: narrator);
        var (admin, learner) = await Setup(f);
        var row = await Create(admin);
        var repeated = await Read<IntroductionDto>(await admin.PostAsJsonAsync($"/api/admin/units/{row.UnitId}/introductions", new CreateIntroductionRequest(row.Id), Json));
        Assert.Equal(row.Id, repeated.Id);
        Assert.Null((await learner.GetFromJsonAsync<IntroductionAvailabilityDto>($"/api/units/{row.UnitId}/introduction", Json))!.Lesson);
        Assert.Equal(HttpStatusCode.Forbidden, (await learner.GetAsync(Root(row.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await learner.GetAsync($"/api/introductions/{row.Id}/audio/0/0")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsJsonAsync(Root(row.Id) + "/audio-next", new IntroductionVersionRequest(row.Version), Json)).StatusCode);
        row = await Fill(admin, row);
        row = await Approve(admin, row);
        Assert.Equal(IntroductionStatus.Approved, row.Status);
        Assert.Empty(narrator.Texts);
        row = await Audio(admin, row);
        Assert.Equal(IntroductionStatus.Ready, row.Status);
        Assert.Null((await learner.GetFromJsonAsync<IntroductionAvailabilityDto>($"/api/units/{row.UnitId}/introduction", Json))!.Lesson);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsJsonAsync(Root(row.Id) + "/publish", new PublishIntroductionRequest(row.Version, false), Json)).StatusCode);
        row = await Publish(admin, row);
        var published = (await learner.GetFromJsonAsync<IntroductionAvailabilityDto>($"/api/units/{row.UnitId}/introduction", Json))!.Lesson!;
        Assert.Equal(row.Id, published.Id);
        var calls = narrator.Texts.Count;
        Assert.Equal("ID3narration"u8.ToArray(), await learner.GetByteArrayAsync("/" + published.Words[0].Beats[0].AudioUrl));
        Assert.Equal(calls, narrator.Texts.Count); // learner reads never generate
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PutAsJsonAsync(Root(row.Id) + $"/words/{row.Words[0].WordId}", Word(row.Version), Json)).StatusCode);
        var next = await Fill(admin, await Create(admin));
        Assert.Equal(2, next.Revision);
        Assert.Equal(row.Id, (await learner.GetFromJsonAsync<IntroductionAvailabilityDto>($"/api/units/{row.UnitId}/introduction", Json))!.Lesson!.Id);
        next = await Publish(admin, await Audio(admin, await Approve(admin, next)));
        Assert.Equal(next.Id, (await learner.GetFromJsonAsync<IntroductionAvailabilityDto>($"/api/units/{row.UnitId}/introduction", Json))!.Lesson!.Id);
        Assert.Equal(calls, narrator.Texts.Count); // same approved text and voice uses the cached complete recording
        Assert.Equal("ID3narration"u8.ToArray(), await learner.GetByteArrayAsync($"/api/introductions/{row.Id}/audio/0/0")); // in-flight older published playback still works
    }

    [Fact]
    public async Task Source_edits_and_stale_versions_cannot_be_approved_or_publish_and_a_stale_draft_can_be_replaced()
    {
        await using var f = Factory();
        var (admin, _) = await Setup(f);
        var row = await Create(admin);
        var stale = row.Version;
        row = await Fill(admin, row);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(Root(row.Id) + "/approve", new IntroductionVersionRequest(stale), Json)).StatusCode);
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var word = await db.CatalogWords.FirstAsync(); word.ContentVersion++; word.TranslationUz = "yangilangan ma’no"; await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(Root(row.Id) + "/approve", new IntroductionVersionRequest(row.Version), Json)).StatusCode);
        await Read<IntroductionDto>(await admin.PostAsJsonAsync(Root(row.Id) + "/discard", new IntroductionVersionRequest(row.Version), Json));
        var replacement = await Create(admin);
        Assert.NotEqual(row.Id, replacement.Id); Assert.False(replacement.SourceChanged);
    }

    [Fact]
    public async Task Ai_partial_or_malformed_output_is_retryable_and_unreviewed_script_cannot_be_approved()
    {
        var ai = new FakeAiProvider(AiProviderKind.Anthropic) { Answer = _ => "{\"words\":[]}" };
        await using var f = Factory(ai);
        var (admin, _) = await Setup(f);
        var row = await Create(admin);
        var bad = await admin.PostAsJsonAsync(Root(row.Id) + "/draft-next", new IntroductionVersionRequest(row.Version), Json);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, bad.StatusCode);
        Assert.Equal(2, (await admin.GetFromJsonAsync<IntroductionDto>(Root(row.Id), Json))!.RemainingWords);
        ai.Answer = _ => JsonSerializer.Serialize(new { words = Enumerable.Range(1, 2).Select(i => new { id = i, title = "Harakatni tushunamiz", beats = Word(0).Beats }) }, Json);
        row = await Read<IntroductionDto>(await admin.PostAsJsonAsync(Root(row.Id) + "/draft-next", new IntroductionVersionRequest(row.Version), Json));
        Assert.Equal(0, row.RemainingWords); Assert.All(row.Words, w => Assert.False(w.Reviewed));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsJsonAsync(Root(row.Id) + "/approve", new IntroductionVersionRequest(row.Version), Json)).StatusCode);
        await using var scope = f.Services.CreateAsyncScope();
        var usage = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AiUsages.OrderBy(u => u.Id).ToListAsync();
        Assert.Equal(2, usage.Count); Assert.Single(usage, u => !u.Succeeded); Assert.Single(usage, u => u.Succeeded);
    }

    [Fact]
    public async Task Failed_recording_does_not_mark_ready_or_skip_its_beat_and_unpublished_or_locked_units_do_not_leak()
    {
        var narrator = new FakeNarrator { Fail = true };
        await using var f = Factory(narrator: narrator);
        var (admin, learner) = await Setup(f);
        var row = await Approve(admin, await Fill(admin, await Create(admin)));
        row = await Read<IntroductionDto>(await admin.PostAsJsonAsync(Root(row.Id) + "/audio-next", new IntroductionVersionRequest(row.Version), Json)); // Korean
        var pending = row.RemainingAudio;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await admin.PostAsJsonAsync(Root(row.Id) + "/audio-next", new IntroductionVersionRequest(row.Version), Json)).StatusCode);
        row = (await admin.GetFromJsonAsync<IntroductionDto>(Root(row.Id), Json))!;
        Assert.Equal(pending, row.RemainingAudio); Assert.Equal(IntroductionStatus.Approved, row.Status);
        narrator.Fail = false; row = await Publish(admin, await Audio(admin, row));
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var unit = await db.Units.SingleAsync(u => u.Id == row.UnitId); unit.Status = UnitStatus.Draft; await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.NotFound, (await learner.GetAsync($"/api/units/{row.UnitId}/introduction")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await learner.GetAsync($"/api/introductions/{row.Id}/audio/0/0")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.CreateClient().GetAsync($"/api/introductions/{row.Id}/audio/0/0")).StatusCode);
    }

    [Fact]
    public async Task A_published_introduction_does_not_bypass_the_existing_unit_lock()
    {
        await using var f = Factory();
        var (admin, learner) = await Setup(f);
        await f.AddWordsAsync(ApiFactory.UserA, 2, ("집", "uy"));
        var row = await Read<IntroductionDto>(await admin.PostAsJsonAsync($"/api/admin/units/{ApiFactory.Unit(2)}/introductions", new CreateIntroductionRequest(Guid.NewGuid()), Json));
        row = await Publish(admin, await Audio(admin, await Approve(admin, await Fill(admin, row))));
        Assert.Equal(HttpStatusCode.Forbidden, (await learner.GetAsync($"/api/units/{row.UnitId}/introduction")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await learner.GetAsync($"/api/introductions/{row.Id}/audio/0/0")).StatusCode);
    }

    [Fact]
    public async Task Re_splitting_can_remove_an_empty_unpublished_unit_and_its_dependent_introduction_draft()
    {
        await using var f = Factory();
        var (admin, _) = await Setup(f);
        var unitId = Guid.NewGuid(); var wordId = Guid.NewGuid();
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Units.Add(new Unit { Id = unitId, Level = CatalogLevel.A, Number = 2, WordCount = 1, CreatedAt = f.Time.GetUtcNow() });
            db.CatalogWords.Add(new CatalogWord { Id = wordId, SourceKey = "intro-test-family", Word = "가족", Pos = "명", Level = CatalogLevel.A,
                IsStudyable = true, UnitId = unitId, OrderInUnit = 1, TranslationUz = "oila", Status = CatalogWordStatus.Reviewed,
                CreatedAt = f.Time.GetUtcNow(), UpdatedAt = f.Time.GetUtcNow() });
            await db.SaveChangesAsync();
        }
        var draft = await Read<IntroductionDto>(await admin.PostAsJsonAsync($"/api/admin/units/{unitId}/introductions", new CreateIntroductionRequest(Guid.NewGuid()), Json));
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.CatalogWords.SingleAsync(w => w.Id == wordId)).IsStudyable = false;
            await db.SaveChangesAsync();
        }
        var split = await admin.PostAsync("/api/admin/catalog/levels/A/split", null);
        Assert.Equal(HttpStatusCode.OK, split.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(Root(draft.Id))).StatusCode);
    }

    [Fact]
    public void A_real_trap_requires_a_deeper_beat_and_a_word_without_a_trap_cannot_get_an_invented_one()
    {
        var source = new IntroductionSource(Guid.NewGuid(), 1, "가다", "bormoq", "gada", null, null, "Yo‘nalishga e’tibor bering.", 1);
        Assert.Throws<Lugat.Application.ValidationException>(() => IntroductionService.CheckWord(source, "Yo‘nalish", Word(0).Beats));
        var withTrap = Word(0).Beats.Append(new IntroductionBeatInput("trap", "Joydan ketish va joyga borishni farqlang.", "회사에 가요.", "Ishga boraman.")).ToList();
        Assert.Equal(3, IntroductionService.CheckWord(source, "Yo‘nalish", withTrap).Beats.Count);
        Assert.Throws<Lugat.Application.ValidationException>(() => IntroductionService.CheckWord(source with { TrapUz = null }, "Yo‘nalish", withTrap));
    }
}
