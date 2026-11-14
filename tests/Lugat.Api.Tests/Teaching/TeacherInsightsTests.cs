using System.Net;
using System.Net.Http.Json;
using Lugat.Api.Tests.Infrastructure;
using Lugat.Contracts;
using Lugat.Domain.Learning;
using Lugat.Domain.Teaching;
using Lugat.Domain.Testing;
using Lugat.Domain.Users;
using Lugat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lugat.Api.Tests.Teaching;

/// <summary>
/// L-73: the group summary, 8-week trends, scores and the "E'tibor bering" list. Weeks are Korea time from Monday
/// 00:00; ApiFactory starts on Tuesday 1 September 2026, 09:00 in Seoul.
/// </summary>
public class TeacherInsightsTests
{
    private const long Teacher = 7001;
    private const long OtherTeacher = 7002;
    private const long LearnerA = 7003;
    private const long LearnerB = 7004;

    private static readonly TimeSpan Seoul = TimeSpan.FromHours(9);

    private static ApiFactory NewFactory() => new() { Premium = false };

    private static async Task<T> OkAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(ApiFactory.Json))!;
    }

    /// <summary>A teacher with a group the given learners joined (each signs in, which marks today active).</summary>
    private static async Task<(Guid GroupId, Guid TeacherId)> GroupAsync(ApiFactory factory, params long[] learners)
    {
        var teacher = await factory.SignedInClientAsync(Teacher, "Ustoz");
        await TeachingTests.GrantAsync(factory, Teacher, UserRoles.Teacher);
        var group = await OkAsync<TeacherGroupDto>(await teacher.PostAsJsonAsync("/api/teacher/groups", new CreateGroupRequest(Guid.NewGuid(), "Seul"), ApiFactory.Json));
        foreach (var id in learners)
        {
            var learner = await factory.SignedInClientAsync(id, $"O‘quvchi {id}");
            await OkAsync<MyGroupDto>(await learner.PostAsync($"/api/groups/join/{group.InviteCode}", null));
        }

        return (group.Id, await TeachingTests.UserIdAsync(factory, Teacher));
    }

    private static async Task<GroupInsightsDto> InsightsAsync(ApiFactory factory, Guid groupId)
    {
        var teacher = await factory.SignedInClientAsync(Teacher, "Ustoz"); // fresh: the clock may have moved past the token
        return await OkAsync<GroupInsightsDto>(await teacher.GetAsync($"/api/teacher/groups/{groupId}/insights"));
    }

    private static async Task SeedAsync(ApiFactory factory, Func<AppDbContext, Task> seed)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await seed(db);
        await db.SaveChangesAsync();
    }

    private static ReviewEvent Review(Guid owner, Card card, DateTimeOffset at, ReviewAnswer answer = ReviewAnswer.Easy, DateTimeOffset? undone = null) => new()
    {
        Id = Guid.NewGuid(), OwnerId = owner, CardId = card.Id, LearningItemId = card.LearningItemId, Direction = card.Direction,
        Answer = answer, OccurredAt = at, UndoneAt = undone,
    };

    /// <summary>A finished unit test of 100 questions, so any whole percent can be scored.</summary>
    private static TestRun UnitTest(Guid owner, int percent, DateTimeOffset finished) => new()
    {
        Id = Guid.NewGuid(), OwnerId = owner, Title = "A-1-bo‘lim · yakuniy test", Status = TestRunStatus.Finished, Purpose = TestPurpose.UnitFinal,
        Questions = [.. Enumerable.Range(0, 100).Select(i => new TestQuestion(i, TestQuestionKind.WordMeaning, "?", null, null, ["a", "b"], "a", null, null, null, false))],
        Answers = [.. Enumerable.Range(0, 100).Select(i => new TestAnswer(i, i < percent ? "a" : "b", i < percent, null, finished))],
        CreatedAt = finished.AddMinutes(-5), FinishedAt = finished,
    };

    [Fact]
    public async Task A_card_counts_once_a_week_and_the_week_turns_on_Monday_00_00_Seoul_time()
    {
        await using var factory = NewFactory();
        var (groupId, _) = await GroupAsync(factory, LearnerA, LearnerB);
        await factory.AddWordsAsync(LearnerA, 1, ApiFactory.Words("가", 2));
        var a = await TeachingTests.UserIdAsync(factory, LearnerA);
        await SeedAsync(factory, async db =>
        {
            var cards = await db.Cards.Where(c => c.OwnerId == a).OrderBy(c => c.Id).Take(2).ToListAsync();
            var (one, two) = (cards[0], cards[1]);
            db.ReviewEvents.AddRange(
                // The week of Monday 31 August: card one three times (the last a second before Monday), card two once.
                Review(a, one, new DateTimeOffset(2026, 9, 1, 10, 0, 0, Seoul).ToUniversalTime()),
                Review(a, one, new DateTimeOffset(2026, 9, 3, 21, 0, 0, Seoul).ToUniversalTime(), ReviewAnswer.Forgot), // "Eslay olmadim" counts
                Review(a, one, new DateTimeOffset(2026, 9, 6, 23, 59, 59, Seoul).ToUniversalTime()),
                Review(a, two, new DateTimeOffset(2026, 9, 2, 8, 0, 0, Seoul).ToUniversalTime()),
                Review(a, two, new DateTimeOffset(2026, 9, 2, 8, 1, 0, Seoul).ToUniversalTime(), ReviewAnswer.TechnicalError),
                // The next week starts at Monday 00:00 in Seoul; an undone answer or a technical error does not count.
                Review(a, one, new DateTimeOffset(2026, 9, 7, 0, 0, 0, Seoul).ToUniversalTime()),
                Review(a, two, new DateTimeOffset(2026, 9, 7, 9, 0, 0, Seoul).ToUniversalTime(), undone: new DateTimeOffset(2026, 9, 7, 9, 0, 5, Seoul).ToUniversalTime()),
                Review(a, two, new DateTimeOffset(2026, 9, 8, 9, 0, 0, Seoul).ToUniversalTime(), ReviewAnswer.TechnicalError));
        });
        factory.Time.SetUtcNow(new DateTimeOffset(2026, 9, 8, 12, 0, 0, Seoul).ToUniversalTime());

        var insights = await InsightsAsync(factory, groupId);
        Assert.Equal(8, insights.Weeks.Count);
        Assert.Equal(new DateOnly(2026, 9, 7), insights.Weeks[7].WeekStart);
        Assert.Equal(new DateOnly(2026, 7, 20), insights.Weeks[0].WeekStart);
        Assert.Equal((2, 2), (insights.Weeks[6].Reviewed, insights.Weeks[6].Active)); // both learners signed in on 1 September
        Assert.Equal((1, 0), (insights.Weeks[7].Reviewed, insights.Weeks[7].Active));
        Assert.All(insights.Weeks.Take(6), w => Assert.Equal((0, 0), (w.Reviewed, w.Active)));
        Assert.Equal((2, 0), (insights.Members, insights.ActiveToday));

        var teacher = await factory.SignedInClientAsync(Teacher, "Ustoz");
        var student = await OkAsync<StudentInsightsDto>(await teacher.GetAsync($"/api/teacher/students/{a}/insights"));
        Assert.Equal((2, 1), (student.Weeks[6].Reviewed, student.Weeks[6].Active)); // one active day
        Assert.Equal((1, 0), (student.Weeks[7].Reviewed, student.Weeks[7].Active));
    }

    [Fact]
    public async Task Not_coming_in_is_flagged_from_the_third_day()
    {
        await using var factory = NewFactory();
        var (groupId, _) = await GroupAsync(factory, LearnerA); // active on 1 September

        factory.Time.SetUtcNow(new DateTimeOffset(2026, 9, 3, 23, 59, 0, Seoul).ToUniversalTime()); // two days later
        Assert.Empty((await InsightsAsync(factory, groupId)).Attention);

        factory.Time.SetUtcNow(new DateTimeOffset(2026, 9, 4, 0, 0, 0, Seoul).ToUniversalTime()); // the third day
        var flagged = Assert.Single((await InsightsAsync(factory, groupId)).Attention);
        Assert.Equal(new RiskFlagDto(RiskKind.Inactive, "3 kun kirmadi"), Assert.Single(flagged.Flags));
    }

    [Fact]
    public async Task A_score_drop_is_flagged_from_15_points()
    {
        await using var factory = NewFactory();
        var (groupId, _) = await GroupAsync(factory, LearnerA, LearnerB);
        var a = await TeachingTests.UserIdAsync(factory, LearnerA);
        var b = await TeachingTests.UserIdAsync(factory, LearnerB);
        var now = factory.Time.GetUtcNow();
        await SeedAsync(factory, db =>
        {
            db.TestRuns.AddRange(UnitTest(a, 80, now.AddHours(-3)), UnitTest(a, 66, now.AddHours(-2)), // 14 points: not yet
                UnitTest(b, 81, now.AddHours(-3)), UnitTest(b, 58, now.AddHours(-2)));                // 23 points
            return Task.CompletedTask;
        });

        var insights = await InsightsAsync(factory, groupId);
        var flagged = Assert.Single(insights.Attention);
        Assert.Equal(b, flagged.UserId);
        Assert.Equal(new RiskFlagDto(RiskKind.ScoreDrop, "Test 81% → 58%"), Assert.Single(flagged.Flags));

        // Exactly 15 points is flagged; the scores chart shows the last tests, highest first, under 60 marked.
        await SeedAsync(factory, db =>
        {
            db.TestRuns.Add(UnitTest(a, 51, now.AddHours(-1))); // 66 → 51
            return Task.CompletedTask;
        });
        insights = await InsightsAsync(factory, groupId);
        Assert.Equal(new RiskFlagDto(RiskKind.ScoreDrop, "Test 66% → 51%"), insights.Attention.Single(x => x.UserId == a).Flags.Single());
        Assert.Equal([(b, 58, true), (a, 51, true)], insights.Scores.Select(s => (s.UserId, s.Percent, s.Low)));
        Assert.Equal((55, (int?)null), (insights.AverageTestPercent, insights.AverageTestChange)); // no test a week ago
        Assert.Equal(60, insights.LowScorePercent);

        // A week later the average is compared with this one.
        factory.Time.Advance(TimeSpan.FromDays(7));
        await SeedAsync(factory, db =>
        {
            db.TestRuns.Add(UnitTest(b, 78, factory.Time.GetUtcNow().AddHours(-1)));
            return Task.CompletedTask;
        });
        insights = await InsightsAsync(factory, groupId);
        Assert.Equal((65, (int?)10), (insights.AverageTestPercent, insights.AverageTestChange)); // (51 + 78) / 2 against 55

        var teacher = await factory.SignedInClientAsync(Teacher, "Ustoz");
        var student = await OkAsync<StudentInsightsDto>(await teacher.GetAsync($"/api/teacher/students/{b}/insights"));
        Assert.Equal([81, 58, 78], student.Tests.Select(t => t.Percent)); // oldest first
    }

    [Fact]
    public async Task A_review_backlog_is_flagged_from_100_waiting_cards()
    {
        await using var factory = NewFactory();
        var (groupId, _) = await GroupAsync(factory, LearnerA);
        await factory.AddWordsAsync(LearnerA, 1, ApiFactory.Words("가", 60));
        var a = await TeachingTests.UserIdAsync(factory, LearnerA);

        async Task DueAsync(int count) => await SeedAsync(factory, async db =>
        {
            var now = factory.Time.GetUtcNow();
            foreach (var (card, i) in (await db.Cards.Where(c => c.OwnerId == a).OrderBy(c => c.Id).ToListAsync()).Select((c, i) => (c, i)))
            {
                (card.Phase, card.DueAt) = i < count ? (LearningPhase.Review, now.AddMinutes(-1)) : (LearningPhase.New, (DateTimeOffset?)null);
            }
        });

        await DueAsync(99);
        Assert.Empty((await InsightsAsync(factory, groupId)).Attention);
        await DueAsync(100);
        var flagged = Assert.Single((await InsightsAsync(factory, groupId)).Attention);
        Assert.Equal(new RiskFlagDto(RiskKind.DueBacklog, "100 karta kutmoqda"), Assert.Single(flagged.Flags));
    }

    [Fact]
    public async Task Homework_is_late_from_its_due_time_and_the_done_card_shows_only_with_a_due_deck()
    {
        await using var factory = NewFactory();
        var (groupId, teacherId) = await GroupAsync(factory, LearnerA);
        Assert.Null((await InsightsAsync(factory, groupId)).Homework);

        var dueAt = new DateTimeOffset(2026, 9, 3, 0, 0, 0, Seoul).ToUniversalTime(); // end of 2 September
        var deckId = Guid.NewGuid();
        await SeedAsync(factory, db =>
        {
            db.GroupDecks.Add(new GroupDeck
            {
                Id = deckId, GroupId = groupId, TeacherUserId = teacherId, Name = "Ish joyi so‘zlari", Status = GroupDeckStatus.Published,
                CreatedAt = factory.Time.GetUtcNow(), PublishedAt = factory.Time.GetUtcNow(), DueOn = new DateOnly(2026, 9, 2), DueAt = dueAt,
            });
            return Task.CompletedTask;
        });

        factory.Time.SetUtcNow(dueAt.AddSeconds(-1));
        var insights = await InsightsAsync(factory, groupId);
        Assert.Equal(new HomeworkSummaryDto(deckId, "Ish joyi so‘zlari", new DateOnly(2026, 9, 2), false, 0, 1, 0), insights.Homework);
        Assert.Empty(insights.Attention);

        factory.Time.SetUtcNow(dueAt);
        insights = await InsightsAsync(factory, groupId);
        Assert.True(insights.Homework!.DuePassed);
        var flagged = Assert.Single(insights.Attention);
        Assert.Equal(new RiskFlagDto(RiskKind.HomeworkLate, "«Ish joyi so‘zlari» vazifasi kechikdi"), Assert.Single(flagged.Flags));

        // Several reasons: the most serious first ("3 kun kirmadi" before the homework).
        factory.Time.SetUtcNow(new DateTimeOffset(2026, 9, 4, 9, 0, 0, Seoul).ToUniversalTime());
        flagged = Assert.Single((await InsightsAsync(factory, groupId)).Attention);
        Assert.Equal([RiskKind.Inactive, RiskKind.HomeworkLate], flagged.Flags.Select(f => f.Kind));
    }

    [Fact]
    public async Task Another_teachers_group_and_learner_stay_hidden_and_a_learner_cannot_open_the_panel()
    {
        await using var factory = NewFactory();
        var (groupId, _) = await GroupAsync(factory, LearnerA);
        var a = await TeachingTests.UserIdAsync(factory, LearnerA);
        var other = await factory.SignedInClientAsync(OtherTeacher, "Boshqa");
        await TeachingTests.GrantAsync(factory, OtherTeacher, UserRoles.Teacher);
        other = await factory.SignedInClientAsync(OtherTeacher, "Boshqa");

        // The existing rule (spec §6.4, §13.8): another teacher's group or learner is 404, as if it did not exist.
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/teacher/groups/{groupId}/insights")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/teacher/students/{a}/insights")).StatusCode);
        var learner = await factory.SignedInClientAsync(LearnerA);
        Assert.Equal(HttpStatusCode.Forbidden, (await learner.GetAsync($"/api/teacher/groups/{groupId}/insights")).StatusCode);
    }

    [Fact]
    public async Task The_answers_payments_and_logins_are_not_in_the_reply()
    {
        await using var factory = NewFactory();
        var (groupId, _) = await GroupAsync(factory, LearnerA);
        var a = await TeachingTests.UserIdAsync(factory, LearnerA);
        var now = factory.Time.GetUtcNow();
        await SeedAsync(factory, db =>
        {
            db.TestRuns.AddRange(UnitTest(a, 90, now.AddHours(-2)), UnitTest(a, 70, now.AddHours(-1)));
            return Task.CompletedTask;
        });

        var teacher = await factory.SignedInClientAsync(Teacher, "Ustoz");
        foreach (var url in new[] { $"/api/teacher/groups/{groupId}/insights", $"/api/teacher/students/{a}/insights" })
        {
            var json = await (await teacher.GetAsync(url)).Content.ReadAsStringAsync();
            Assert.Contains("70", json); // the score is there
            foreach (var hidden in new[] { "\"answer", "\"chosen", "\"questions", "\"amount", "\"payment", "\"promo", "telegram", "\"login", "\"email" })
            {
                Assert.DoesNotContain(hidden, json, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public async Task The_group_summary_costs_the_same_queries_for_two_learners_as_for_six()
    {
        await using var factory = NewFactory();
        var (groupId, teacherId) = await GroupAsync(factory);
        string invite;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            invite = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Groups.Where(g => g.Id == groupId).Select(g => g.InviteCode).SingleAsync();
        }
        await SeedAsync(factory, db =>
        {
            var now = factory.Time.GetUtcNow();
            db.GroupDecks.Add(new GroupDeck
            {
                Id = Guid.NewGuid(), GroupId = groupId, TeacherUserId = teacherId, Name = "1-dars", Status = GroupDeckStatus.Published,
                CreatedAt = now, PublishedAt = now, DueOn = new DateOnly(2026, 9, 1), DueAt = now.AddHours(-1),
            });
            return Task.CompletedTask;
        });
        using var sql = new SqlCounter(factory.DatabaseName);

        async Task<int> CountAsync(int from, int to)
        {
            for (var n = from; to > n; n++)
            {
                var id = 7100 + n;
                var learner = await factory.SignedInClientAsync(id, $"O‘quvchi {n}");
                await OkAsync<MyGroupDto>(await learner.PostAsync($"/api/groups/join/{invite}", null));
                await factory.AddWordsAsync(id, 1, ApiFactory.Words("가", 2));
                var userId = await TeachingTests.UserIdAsync(factory, id);
                await SeedAsync(factory, async db =>
                {
                    var card = await db.Cards.FirstAsync(c => c.OwnerId == userId);
                    db.ReviewEvents.Add(Review(userId, card, factory.Time.GetUtcNow().AddMinutes(-5)));
                    db.TestRuns.AddRange(UnitTest(userId, 90, factory.Time.GetUtcNow().AddHours(-2)), UnitTest(userId, 60, factory.Time.GetUtcNow().AddHours(-1)));
                });
            }

            var teacher = await factory.SignedInClientAsync(Teacher, "Ustoz");
            await teacher.GetAsync("/api/me");
            sql.Reset();
            var insights = await OkAsync<GroupInsightsDto>(await teacher.GetAsync($"/api/teacher/groups/{groupId}/insights"));
            var count = sql.Count;
            Assert.Equal(to, insights.Attention.Count); // each: score drop and late homework
            Assert.Equal(to, insights.Weeks[^1].Reviewed);
            return count;
        }

        Assert.Equal(await CountAsync(0, 2), await CountAsync(2, 6));
    }
}
