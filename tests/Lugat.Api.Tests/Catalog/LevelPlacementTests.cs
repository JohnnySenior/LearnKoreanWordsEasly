using System.Net;
using System.Net.Http.Json;
using Lugat.Api.Tests.Ai;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Application.Catalog;
using Lugat.Contracts;
using Lugat.Domain.Ai;
using Lugat.Domain.Catalog;
using Lugat.Domain.Testing;
using Lugat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lugat.Api.Tests.Catalog;

/// <summary>L-90: the exam that opens level B for a learner who knows level A.</summary>
public class LevelPlacementTests
{
    /// <summary>Level A: 75 words (units of 25), level B: 25 words (one unit); all published by the content job.</summary>
    private static async Task<ApiFactory> FactoryAsync()
    {
        var ai = new FakeAiProvider(AiProviderKind.Anthropic) { Answer = r => ContentAutopilotTests.Answer(r, null) };
        var f = new ApiFactory { AdminTelegramIds = [ApiFactory.UserA], AiProviders = [ai] };
        var admin = await f.SignedInClientAsync(ApiFactory.UserA);
        List<ListRow> rows = [.. Enumerable.Range(1, 100).Select(i => new ListRow(i * 2, $"{(char)('가' + i)}다", "동", null, i <= 75 ? "A" : "B"))];
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync("/api/admin/catalog/import", CatalogFiles.Upload(CatalogFiles.Xlsx(rows), "list.xlsx"))).StatusCode);
        Guid adminId;
        await using (var scope = f.Services.CreateAsyncScope())
        {
            adminId = (await scope.ServiceProvider.GetRequiredService<AppDbContext>().UserLogins.SingleAsync(l => l.ProviderUserId == ApiFactory.UserA.ToString())).UserId;
        }

        foreach (var level in new[] { CatalogLevel.A, CatalogLevel.B })
        {
            AutopilotStep? step = null;
            for (var i = 0; i < 60 && step is not { Finished: true }; i++)
            {
                await using var scope = f.Services.CreateAsyncScope();
                step = await scope.ServiceProvider.GetRequiredService<ContentAutopilotService>().StepAsync(adminId, level, CancellationToken.None);
            }

            Assert.False(step!.Stopped, step.Message);
        }

        return f;
    }

    private static async Task<T> Ok<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(ApiFactory.Json))!;
    }

    /// <summary>Answers the exam, right where <paramref name="right"/> says so (by question index and unit number), then finishes.</summary>
    private static async Task<TestDto> TakeAsync(ApiFactory f, HttpClient learner, Guid testId, Func<int, int, bool> right)
    {
        List<(TestQuestion Question, int Unit)> questions;
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var run = await db.TestRuns.SingleAsync(r => r.Id == testId);
            var items = run.Questions.Select(q => q.LearningItemId!.Value).ToList();
            var numbers = await (from i in db.LearningItems join u in db.Units on i.UnitId equals u.Id where items.Contains(i.Id) select new { i.Id, u.Number }).ToDictionaryAsync(x => x.Id, x => x.Number);
            questions = run.Questions.Select(q => (q, numbers[q.LearningItemId!.Value])).ToList();
        }

        Assert.Equal(questions.Select(q => q.Unit).Order(), questions.Select(q => q.Unit)); // easy to hard
        foreach (var (q, unit) in questions)
        {
            var answer = right(q.Index, unit) ? q.Correct : q.Options.First(o => o != q.Correct);
            await Ok<TestDto>(await learner.PostAsJsonAsync($"/api/tests/{testId}/answers", new TestAnswerRequest(q.Index, answer), ApiFactory.Json));
        }

        return await Ok<TestDto>(await learner.PostAsync($"/api/tests/{testId}/finish", null));
    }

    [Fact]
    public async Task Passing_opens_level_B_marks_level_A_passed_and_adds_cards_only_for_the_words_answered_wrong()
    {
        await using var f = await FactoryAsync();
        var learner = await f.SignedInClientAsync(ApiFactory.UserB);
        var units = await learner.GetFromJsonAsync<LevelUnitsDto>("/api/levels/B/units", ApiFactory.Json);
        Assert.Equal((CatalogLevel.A, 40, 85), (units!.Placement!.From, units.Placement.Questions, units.Placement.PassPercent));
        Assert.Null((await learner.GetFromJsonAsync<LevelUnitsDto>("/api/levels/A/units", ApiFactory.Json))!.Placement);

        var test = await Ok<TestDto>(await learner.PostAsJsonAsync("/api/levels/B/placement", new StartPlacementRequest(Guid.NewGuid()), ApiFactory.Json));
        Assert.Equal(40, test.Questions.Count);
        Assert.Equal(34, test.Unit!.PassMark);
        Assert.Equal(CatalogLevel.B, test.Unit.Placement!.To);
        var result = await TakeAsync(f, learner, test.Id, (index, _) => index != 3 && index != 7);

        Assert.True(result.Unit!.Passed);
        Assert.Equal(3, result.Unit.Placement!.KnownUnits);
        var levelB = (await learner.GetFromJsonAsync<LevelUnitsDto>("/api/levels/B/units", ApiFactory.Json))!;
        Assert.Equal(levelB.Units[0].Id, result.Unit.NextUnitId);
        Assert.Equal(UnitState.Open, levelB.Units[0].State);
        Assert.Null(levelB.Placement);
        Assert.All((await learner.GetFromJsonAsync<LevelUnitsDto>("/api/levels/A/units", ApiFactory.Json))!.Units, u => Assert.Equal(UnitState.Passed, u.State));
        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var learnerId = (await db.UserLogins.SingleAsync(l => l.ProviderUserId == ApiFactory.UserB.ToString())).UserId;
        var aItems = await db.LearningItems.Where(i => db.Units.Any(u => u.Id == i.UnitId && u.Level == CatalogLevel.A)).Select(i => i.Id).ToListAsync();
        // Two words answered wrong, two cards each; the other level A words add nothing to the reviews.
        Assert.Equal(4, await db.Cards.CountAsync(c => c.OwnerId == learnerId && aItems.Contains(c.LearningItemId)));
        Assert.All(await db.UnitProgress.Where(p => p.UserId == learnerId && p.Status == UnitProgressStatus.Passed).ToListAsync(), p => Assert.Equal(UnitPassedBy.Placement, p.PassedBy));
    }

    [Fact]
    public async Task A_fail_says_how_many_first_units_are_known_and_opens_them_on_request_and_two_tries_a_day()
    {
        await using var f = await FactoryAsync();
        var learner = await f.SignedInClientAsync(ApiFactory.UserB);
        var test = await Ok<TestDto>(await learner.PostAsJsonAsync("/api/levels/B/placement", new StartPlacementRequest(Guid.NewGuid()), ApiFactory.Json));
        // Knows units 1 and 2 of level A, not unit 3.
        var result = await TakeAsync(f, learner, test.Id, (_, unit) => unit < 3);

        Assert.False(result.Unit!.Passed);
        Assert.Equal((2, false), (result.Unit.Placement!.KnownUnits, result.Unit.Placement.KnownOpened));
        Assert.Null(result.Unit.NextUnitId);
        Assert.Equal(UnitState.Locked, (await learner.GetFromJsonAsync<LevelUnitsDto>("/api/levels/A/units", ApiFactory.Json))!.Units[2].State);

        var opened = await Ok<TestDto>(await learner.PostAsync($"/api/placement/{test.Id}/open-known", null));
        var levelA = (await learner.GetFromJsonAsync<LevelUnitsDto>("/api/levels/A/units", ApiFactory.Json))!;
        Assert.Equal([UnitState.Passed, UnitState.Passed, UnitState.Open], levelA.Units.Select(u => u.State));
        Assert.Equal(levelA.Units[2].Id, opened.Unit!.NextUnitId);
        Assert.True(opened.Unit.Placement!.KnownOpened);
        Assert.NotNull((await learner.GetFromJsonAsync<LevelUnitsDto>("/api/levels/B/units", ApiFactory.Json))!.Placement); // B is still not open

        Assert.True((await learner.PostAsJsonAsync("/api/levels/B/placement", new StartPlacementRequest(Guid.NewGuid()), ApiFactory.Json)).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await learner.PostAsJsonAsync("/api/levels/B/placement", new StartPlacementRequest(Guid.NewGuid()), ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await learner.PostAsJsonAsync("/api/levels/A/placement", new StartPlacementRequest(Guid.NewGuid()), ApiFactory.Json)).StatusCode);
    }

    [Fact]
    public void Known_units_are_the_longest_well_answered_run_from_the_start()
    {
        Assert.Equal(0, LevelPlacementService.KnownUnits([(1, true), (1, true), (2, false), (2, true)]));
        Assert.Equal(2, LevelPlacementService.KnownUnits([(1, true), (1, true), (2, true), (2, true), (3, false), (3, false)]));
        Assert.Equal(3, LevelPlacementService.KnownUnits([.. Enumerable.Repeat((1, true), 6), (2, true), (2, true), (3, false), (3, true), (3, true), (3, true), (4, false), (4, false)]));
    }
}
