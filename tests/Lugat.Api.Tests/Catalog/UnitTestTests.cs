using System.Net;
using System.Net.Http.Json;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Api.Tests.Speech;
using Lugat.Contracts;
using Lugat.Domain.Catalog;
using Lugat.Domain.Learning;
using Lugat.Domain.Testing;
using Lugat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lugat.Api.Tests.Catalog;

/// <summary>Stage 4 (spec §4.4, §4.5, §13.5): the unit test and "Bilaman".</summary>
public class UnitTestTests
{
    private readonly FakeSpeech _speech = new();

    /// <summary>Units A-1 … A-3 of 25 words each; the learner has A-1 open with its cards.</summary>
    private async Task<(ApiFactory Factory, HttpClient Client)> SetUpAsync(bool speech = true)
    {
        var factory = new ApiFactory { Speech = speech ? _speech : null };
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await factory.AddWordsAsync(ApiFactory.UserA, 1, ApiFactory.Words("가", 25));
        await using var scope = factory.Services.CreateAsyncScope();
        await ApiFactory.PublishUnitAsync(scope.ServiceProvider, 2, ApiFactory.Words("나", 25));
        await ApiFactory.PublishUnitAsync(scope.ServiceProvider, 3, ApiFactory.Words("다", 25));
        return (factory, client);
    }

    private static async Task<HttpResponseMessage> StartAsync(HttpClient client, int unit, UnitTestKind kind, Guid? id = null) =>
        await client.PostAsJsonAsync($"/api/units/{ApiFactory.Unit(unit)}/tests", new StartUnitTestRequest(id ?? Guid.NewGuid(), kind), ApiFactory.Json);

    private static async Task<TestDto> StartOkAsync(HttpClient client, int unit, UnitTestKind kind = UnitTestKind.Final)
    {
        var response = await StartAsync(client, unit, kind);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TestDto>(ApiFactory.Json))!;
    }

    /// <summary>Answers <paramref name="right"/> questions right and the rest wrong, reading the answers from the database.</summary>
    private static async Task AnswerAsync(ApiFactory factory, HttpClient client, Guid testId, int right)
    {
        List<TestQuestion> questions;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            questions = (await scope.ServiceProvider.GetRequiredService<AppDbContext>().TestRuns.SingleAsync(r => r.Id == testId)).Questions;
        }

        foreach (var q in questions)
        {
            var answer = q.Index < right ? q.Correct : q.Options.Count == 0 ? "틀린답" : q.Options.First(o => o != q.Correct); // typed: any wrong word
            (await client.PostAsJsonAsync($"/api/unit-tests/{testId}/answers", new TestAnswerRequest(q.Index, answer), ApiFactory.Json)).EnsureSuccessStatusCode();
        }
    }

    private static async Task<TestDto> FinishAsync(HttpClient client, Guid testId) =>
        (await (await client.PostAsync($"/api/unit-tests/{testId}/finish", null)).Content.ReadFromJsonAsync<TestDto>(ApiFactory.Json))!;

    private static async Task<LevelUnitsDto> UnitsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<LevelUnitsDto>("/api/levels/A/units", ApiFactory.Json))!;

    [Fact]
    public async Task Twenty_of_twenty_five_passes_and_opens_the_next_unit()
    {
        var (factory, client) = await SetUpAsync();
        await using var _ = factory;

        var test = await StartOkAsync(client, 1);
        await AnswerAsync(factory, client, test.Id, 20);
        var result = await FinishAsync(client, test.Id);
        var units = await UnitsAsync(client);

        Assert.Equal(25, test.Questions.Count);
        Assert.Equal(20, test.Unit!.PassMark);
        Assert.Equal((true, 20), (result.Unit!.Passed, result.Score));
        Assert.Equal(ApiFactory.Unit(2), result.Unit.NextUnitId);
        Assert.Equal(5, result.Unit.WrongItemIds.Count);
        Assert.Equal([(UnitState.Passed, (int?)80), (UnitState.Open, null), (UnitState.Locked, null)], units.Units.Select(u => (u.State, u.BestScorePercent)));
    }

    [Fact]
    public async Task Nineteen_of_twenty_five_does_not_pass_and_a_retake_draws_anew()
    {
        var (factory, client) = await SetUpAsync();
        await using var _ = factory;

        var first = await StartOkAsync(client, 1);
        await AnswerAsync(factory, client, first.Id, 19);
        var result = await FinishAsync(client, first.Id);
        var units = await UnitsAsync(client);
        var retake = await StartOkAsync(client, 1);

        Assert.Equal((false, 19), (result.Unit!.Passed, result.Score));
        Assert.Null(result.Unit.NextUnitId);
        Assert.Equal(6, result.Unit.WrongItemIds.Count);
        Assert.Equal([UnitState.Open, UnitState.Locked, UnitState.Locked], units.Units.Select(u => u.State));
        Assert.Equal(76, units.Units[0].BestScorePercent);
        Assert.NotEqual(first.Questions.Select(q => (q.Prompt, q.Kind)), retake.Questions.Select(q => (q.Prompt, q.Kind)));
    }

    [Fact]
    public async Task Finishing_twice_applies_the_result_once()
    {
        var (factory, client) = await SetUpAsync();
        await using var _ = factory;
        var test = await StartOkAsync(client, 1);
        await AnswerAsync(factory, client, test.Id, 25);

        var finishes = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => client.PostAsync($"/api/unit-tests/{test.Id}/finish", null)));
        var again = await FinishAsync(client, test.Id);
        var late = await client.PostAsJsonAsync($"/api/unit-tests/{test.Id}/answers", new TestAnswerRequest(0, "x"), ApiFactory.Json);

        Assert.All(finishes, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.True(again.Unit!.Passed);
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode); // answers cannot change after the end
        await using var scope = factory.Services.CreateAsyncScope();
        var progress = await scope.ServiceProvider.GetRequiredService<AppDbContext>().UnitProgress.SingleAsync(p => p.UnitId == ApiFactory.Unit(1));
        Assert.Equal((1, UnitPassedBy.Test, (int?)100), (progress.Attempts, progress.PassedBy, progress.BestScorePercent));
    }

    [Fact]
    public async Task Answers_stay_hidden_until_the_end_and_listening_sends_no_text()
    {
        var (factory, client) = await SetUpAsync();
        await using var _ = factory;

        var response = await StartAsync(client, 1, UnitTestKind.Final);
        var json = await response.Content.ReadAsStringAsync();
        var test = (await response.Content.ReadFromJsonAsync<TestDto>(ApiFactory.Json))!;
        var listening = test.Questions.Where(q => q.Kind is TestQuestionKind.WordListening or TestQuestionKind.WordListeningTyped).ToList();
        var audio = await client.GetAsync($"/api/tests/{test.Id}/questions/{listening[0].Index}/audio");
        var notListening = await client.GetAsync($"/api/tests/{test.Id}/questions/{test.Questions.First(q => q.Kind is TestQuestionKind.WordMeaning).Index}/audio");
        var other = await (await factory.SignedInClientAsync(ApiFactory.UserB)).GetAsync($"/api/tests/{test.Id}");

        // L-38: 11 of 25 are typed, so options alone do not pass the unit.
        Assert.Equal([(TestQuestionKind.WordMeaning, 6), (TestQuestionKind.WordKorean, 6), (TestQuestionKind.WordListening, 2),
                (TestQuestionKind.WordKoreanTyped, 8), (TestQuestionKind.WordListeningTyped, 3)],
            test.Questions.GroupBy(q => q.Kind).OrderBy(g => g.Key).Select(g => (g.Key, g.Count())));
        Assert.All(test.Questions, q => Assert.Null(q.Correct));
        Assert.All(test.Questions, q => Assert.Equal(TestGrader.IsTyped(q.Kind) ? 0 : 4, q.Options.Distinct().Count()));
        Assert.All(test.Questions.Where(q => q.Kind == TestQuestionKind.WordListeningTyped), q => Assert.DoesNotContain("가", q.Prompt));
        Assert.All(listening, q => Assert.Null(q.Translation));
        Assert.Equal(25, test.Questions.Select(q => q.Index).Distinct().Count());
        Assert.Equal(HttpStatusCode.OK, audio.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, notListening.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        // The heard word is in the options, but nothing in the response says which one it is.
        Assert.DoesNotContain("\"correct\":\"", json);
        Assert.Single(_speech.Texts);
    }

    [Fact]
    public async Task Without_speech_listening_becomes_meaning_and_the_test_says_so()
    {
        var (factory, client) = await SetUpAsync(speech: false);
        await using var _ = factory;

        var test = await StartOkAsync(client, 1);

        Assert.DoesNotContain(test.Questions, q => TestGrader.IsListening(q.Kind));
        Assert.Equal(8, test.Questions.Count(q => q.Kind == TestQuestionKind.WordMeaning));
        Assert.Equal(10, test.Questions.Count(q => q.Kind == TestQuestionKind.WordKoreanTyped));
        Assert.Contains("tinglash", test.Warning);
    }

    [Fact]
    public async Task The_unit_test_does_not_change_the_cards()
    {
        var (factory, client) = await SetUpAsync();
        await using var _ = factory;
        async Task<List<(Guid, LearningPhase, DateTimeOffset?)>> CardsAsync()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            return (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Cards.OrderBy(c => c.Id).ToListAsync())
                .Select(c => (c.Id, c.Phase, c.DueAt)).ToList();
        }

        var before = await CardsAsync();
        var test = await StartOkAsync(client, 1);
        await AnswerAsync(factory, client, test.Id, 10);
        await FinishAsync(client, test.Id);
        var mistakes = (await client.GetFromJsonAsync<MistakesDto>("/api/mistakes", ApiFactory.Json))!;

        Assert.Equal(before, await CardsAsync()); // TEST04: choosing an option is no proof of recall
        Assert.Equal(15, mistakes.Words.Count);
        Assert.All(mistakes.Words, w => Assert.Contains("bo‘lim testi", w.Sources));
    }

    [Fact]
    public async Task After_the_last_published_unit_the_home_page_says_so_and_the_next_unit_opens_once_published()
    {
        // Launch with part of level A published: a learner who passes them all is told why nothing is open.
        var factory = new ApiFactory { Speech = _speech };
        await using var _ = factory;
        var client = await factory.SignedInClientAsync(ApiFactory.UserA);
        await factory.AddWordsAsync(ApiFactory.UserA, 1, ApiFactory.Words("가", 25));

        var test = await StartOkAsync(client, 1);
        await AnswerAsync(factory, client, test.Id, 25);
        await FinishAsync(client, test.Id);
        var allDone = await client.GetFromJsonAsync<TodayDto>("/api/today", ApiFactory.Json);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await ApiFactory.PublishUnitAsync(scope.ServiceProvider, 2, ApiFactory.Words("나", 25));
        }

        var published = await client.GetFromJsonAsync<TodayDto>("/api/today", ApiFactory.Json);

        Assert.Equal((null, 1), (allDone!.CurrentUnit, allDone.PassedUnits));
        Assert.Equal(ApiFactory.Unit(2), published!.CurrentUnit?.Id);
    }

    [Fact]
    public async Task Bilaman_is_only_for_the_next_locked_unit_three_times_a_day()
    {
        var (factory, client) = await SetUpAsync();
        await using var _ = factory;

        var notNext = await StartAsync(client, 3, UnitTestKind.Skip);
        var openUnit = await StartAsync(client, 1, UnitTestKind.Skip);
        var finalOfLocked = await StartAsync(client, 2, UnitTestKind.Final);
        var units = await UnitsAsync(client);
        var attempts = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            attempts.Add((await StartAsync(client, 2, UnitTestKind.Skip)).StatusCode);
        }

        factory.Time.Advance(TimeSpan.FromDays(1));
        client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var tomorrow = await StartAsync(client, 2, UnitTestKind.Skip);

        Assert.Equal(HttpStatusCode.Forbidden, notNext.StatusCode); // to skip unit 3, unit 2 first
        Assert.Equal(HttpStatusCode.Forbidden, openUnit.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, finalOfLocked.StatusCode);
        Assert.Equal([false, true, false], units.Units.Select(u => u.CanSkip));
        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], attempts);
        Assert.Equal(HttpStatusCode.OK, tomorrow.StatusCode);
    }

    [Fact]
    public async Task Parallel_Bilaman_starts_with_different_ids_never_pass_the_daily_limit()
    {
        // Audit 2026-10-26: each start read the day's count before any of them was saved, so all got through.
        var (factory, client) = await SetUpAsync();
        await using var _ = factory;
        var ids = Enumerable.Range(0, 8).Select(_ => Guid.NewGuid()).ToList();

        var responses = await Task.WhenAll(ids.Select(id => StartAsync(client, 2, UnitTestKind.Skip, id)));
        var started = ids.Where((_, i) => responses[i].StatusCode == HttpStatusCode.OK).ToList();
        var again = await StartAsync(client, 2, UnitTestKind.Skip, started[0]); // the same id is the same attempt
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(3, await db.TestRuns.CountAsync(r => r.UnitId == ApiFactory.Unit(2) && r.Purpose == TestPurpose.UnitSkip));
        }

        factory.Time.Advance(TimeSpan.FromDays(1));
        client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var tomorrow = await StartAsync(client, 2, UnitTestKind.Skip);

        Assert.Equal(3, started.Count);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.OK), r => Assert.Equal(HttpStatusCode.TooManyRequests, r.StatusCode));
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(started[0], (await again.Content.ReadFromJsonAsync<TestDto>(ApiFactory.Json))!.Id);
        Assert.Equal(HttpStatusCode.OK, tomorrow.StatusCode);
    }

    [Fact]
    public async Task The_daily_Bilaman_limit_follows_the_learners_chosen_time_zone()
    {
        // Audit 2026-10-26: a learner in Tashkent had Seoul's midnight (20:00 or 19:00 at home) as the day boundary.
        var (factory, client) = await SetUpAsync();
        await using var _ = factory;
        factory.Time.SetUtcNow(new DateTimeOffset(2027, 1, 4, 23, 0, 0, TimeSpan.FromHours(9)).ToUniversalTime()); // Seoul 23:00 = Tashkent 19:00
        client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var zone = await client.PatchAsJsonAsync("/api/me/preferences", new UpdatePreferencesRequest(null, null, null, "Asia/Tashkent"), ApiFactory.Json);
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await StartAsync(client, 2, UnitTestKind.Skip)).StatusCode);
        }

        factory.Time.Advance(TimeSpan.FromHours(2)); // a new day in Seoul, still 21:00 in Tashkent
        client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var sameDay = await StartAsync(client, 2, UnitTestKind.Skip);
        factory.Time.Advance(TimeSpan.FromHours(3)); // 00:00 in Tashkent
        client = await factory.SignedInClientAsync(ApiFactory.UserA);
        var nextDay = await StartAsync(client, 2, UnitTestKind.Skip);

        Assert.Equal("Asia/Tashkent", (await zone.Content.ReadFromJsonAsync<PreferencesDto>(ApiFactory.Json))!.TimeZone);
        Assert.Equal(HttpStatusCode.TooManyRequests, sameDay.StatusCode);
        Assert.Equal(HttpStatusCode.OK, nextDay.StatusCode);
    }

    [Fact]
    public async Task A_passed_Bilaman_test_opens_the_unit_with_known_words_as_reviews()
    {
        var (factory, client) = await SetUpAsync();
        await using var _ = factory;

        var failed = await StartOkAsync(client, 2, UnitTestKind.Skip);
        await AnswerAsync(factory, client, failed.Id, 19);
        var failure = await FinishAsync(client, failed.Id);
        var stillLocked = await client.GetAsync($"/api/units/{ApiFactory.Unit(2)}");

        var test = await StartOkAsync(client, 2, UnitTestKind.Skip);
        await AnswerAsync(factory, client, test.Id, 21);
        var result = await FinishAsync(client, test.Id);
        var units = await UnitsAsync(client);

        Assert.False(failure.Unit!.Passed);
        Assert.Equal(HttpStatusCode.Forbidden, stillLocked.StatusCode);
        Assert.True(result.Unit!.Passed);
        Assert.Equal(ApiFactory.Unit(3), result.Unit.NextUnitId);
        Assert.Equal([UnitState.Open, UnitState.Passed, UnitState.Open], units.Units.Select(u => u.State));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cards = await (from c in db.Cards join i in db.LearningItems on c.LearningItemId equals i.Id where i.UnitId == ApiFactory.Unit(2) select c).ToListAsync();
        Assert.Equal(50, cards.Count);
        Assert.Equal(42, cards.Count(c => c.Phase == LearningPhase.Review && c.Stage == 1 && c.DueAt == factory.Time.GetUtcNow().AddDays(1)));
        Assert.Equal(8, cards.Count(c => c.Phase == LearningPhase.New));
        Assert.All(cards, c => Assert.Equal(0, c.IndependentSuccessDays));
        Assert.Equal(UnitPassedBy.SkipTest, (await db.UnitProgress.SingleAsync(p => p.UnitId == ApiFactory.Unit(2))).PassedBy);
    }

    [Fact]
    public async Task Which_word_is_right_uses_the_checked_catalog_questions_without_AI()
    {
        var (factory, client) = await SetUpAsync();
        await using var _ = factory;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var word = await db.CatalogWords.SingleAsync(w => w.UnitId == ApiFactory.Unit(1) && w.OrderInUnit == 1);
            db.CatalogUsages.Add(new CatalogUsage
            {
                Id = Guid.NewGuid(),
                CatalogWordId = word.Id,
                ContentVersion = 1,
                Questions = [new("Bu maktab.", "이것은 ___입니다.", word.Word, ["집"], "Maktab — shu so‘z.")],
            });
            await db.SaveChangesAsync();
        }

        var summary = (await client.GetFromJsonAsync<List<UsageTableSummaryDto>>("/api/vocabulary/usage", ApiFactory.Json))!;
        var created = await client.PostAsJsonAsync("/api/tests", new CreateTestRequest(Guid.NewGuid(), [ApiFactory.Unit(1)], 10, Usage: true), ApiFactory.Json);
        var test = (await created.Content.ReadFromJsonAsync<TestDto>(ApiFactory.Json))!;

        Assert.Equal((1, 1), (summary[0].Ready, summary[0].Questions));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("이것은 ___입니다.", Assert.Single(test.Questions).Sentence);
    }
}
