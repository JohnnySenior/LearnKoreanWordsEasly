using System.Linq.Expressions;
using Lugat.Application.Abstractions;
using Lugat.Contracts;
using Lugat.Domain;
using Lugat.Domain.Learning;
using Lugat.Domain.Teaching;
using Lugat.Domain.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Lugat.Application.Teaching;

/// <summary>Thresholds of the teacher's group summary and risk flags (L-73), section "Teaching".</summary>
public sealed class TeachingOptions
{
    public const string SectionName = "Teaching";

    /// <summary>A last unit test below this many percent shows in another colour on the scores chart.</summary>
    public int LowScorePercent { get; set; } = 60;

    public TeachingRiskOptions Risk { get; set; } = new();
}

public sealed class TeachingRiskOptions
{
    /// <summary>"Kirmayapti" from this many days since the last active day (Korea time).</summary>
    public int InactiveDays { get; set; } = 3;

    /// <summary>"Ball tushdi" when the last unit test is this many points or more below the one before.</summary>
    public int ScoreDropPoints { get; set; } = 15;

    /// <summary>"Takror to‘planib qoldi" from this many cards waiting for review.</summary>
    public int DueBacklog { get; set; } = 100;

    /// <summary>"Vazifa kechikdi" stays on the list this many days after the deck's due time, then drops off.</summary>
    public int HomeworkLateDays { get; set; } = 14;
}

/// <summary>
/// The teacher's view over time (L-73, spec §9.2): a group summary, 8-week trends, last unit test scores and who may
/// need attention, and the same for one learner. Built from data already kept (activity days, review events, cards
/// due, unit test runs, L-71 deck completion); counts and scores only — never answers, payments or logins. The group
/// page reads a fixed number of grouped queries, whatever the group's size.
/// </summary>
public sealed class TeacherInsightsService(IAppDbContext db, TimeProvider time, IOptions<TeachingOptions> options)
{
    public const int Weeks = 8;

    private TeachingOptions Options => options.Value;

    public async Task<GroupInsightsDto> GroupAsync(Guid teacherId, Guid groupId, CancellationToken ct)
    {
        _ = await db.Groups.AsNoTracking().SingleOrDefaultAsync(g => g.Id == groupId && g.TeacherUserId == teacherId && g.ClosedAt == null, ct)
            ?? throw new NotFoundException("Guruh topilmadi.");
        var members = await (from m in db.GroupMembers
                             join u in db.Users on m.UserId equals u.Id
                             where m.GroupId == groupId && m.LeftAt == null && u.DeletedAt == null
                             select new Member(u.Id, u.DisplayName, m.JoinedAt)).ToListAsync(ct);
        var ids = members.Select(m => m.Id).ToList();
        var now = time.GetUtcNow();
        var today = KoreaTime.Day(now);
        var weeks = WeekStarts(today);

        var days = await ActivityAsync(ids, weeks[0], ct);
        var reviewed = await ReviewedByWeekAsync(ids, weeks, now, ct);
        var tests = await UnitTestsAsync(ids, ct);
        var due = await TeacherService.DueOfAsync(db, ids, now, ct);
        var lastActive = await LastActiveAsync(ids, ct);

        // The deck shown on the card: the latest due day among the group's published decks.
        var decks = await db.GroupDecks.AsNoTracking()
            .Where(d => d.GroupId == groupId && d.Status == GroupDeckStatus.Published && d.DueAt != null).ToListAsync(ct);
        HomeworkSummaryDto? homework = null;
        if (decks.OrderByDescending(d => d.DueAt).FirstOrDefault() is { } latest)
        {
            var done = await GroupDeckReport.DoneAsync(db, latest.Id, ids, GroupDeckReport.Cutoff(latest, now), ct);
            var count = ids.Count(id => done.TryGetValue(id, out var d) && d.Done);
            homework = new HomeworkSummaryDto(latest.Id, latest.Name, latest.DueOn!.Value, latest.DueAt <= now, count, ids.Count,
                ids.Count == 0 ? 0 : (int)Math.Round(count * 100.0 / ids.Count, MidpointRounding.AwayFromZero));
        }

        var late = await LateHomeworkAsync(decks, ids, now, ct);

        var weekPoints = weeks.Select((start, i) => new WeekPointDto(start,
            reviewed.Where(r => r.Week == i).Sum(r => r.Count),
            days.Where(d => WeekOf(weeks, d.Day) == i).Select(d => d.UserId).Distinct().Count())).ToList();

        var lastScores = tests.ToLookup(t => t.OwnerId);
        var current = ids.Select(id => lastScores[id].LastOrDefault()).OfType<TestRow>().ToList();
        var weekAgo = now.AddDays(-7);
        var before = ids.Select(id => lastScores[id].LastOrDefault(t => t.At <= weekAgo)).OfType<TestRow>().ToList();
        int? average = current.Count == 0 ? null : (int)Math.Round(current.Average(t => t.Percent), MidpointRounding.AwayFromZero);
        int? change = average is { } a && before.Count > 0 ? a - (int)Math.Round(before.Average(t => t.Percent), MidpointRounding.AwayFromZero) : null;

        var names = members.ToDictionary(m => m.Id, m => m.Name);
        var scores = current.Select(t => new StudentScoreDto(t.OwnerId, names[t.OwnerId], t.Percent, t.Percent < Options.LowScorePercent))
            .OrderByDescending(s => s.Percent).ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

        var attention = members
            .Select(m => new StudentRiskDto(m.Id, m.Name, Flags(m, today, lastActive, lastScores[m.Id].ToList(), due.GetValueOrDefault(m.Id), late[m.Id])))
            .Where(s => s.Flags.Count > 0)
            .OrderBy(s => s.Flags.Min(f => (int)f.Kind)).ThenByDescending(s => s.Flags.Count)
            .ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new GroupInsightsDto(ids.Count, days.Count(d => d.Day == today), homework, average, change, weekPoints, scores, attention, Options.LowScorePercent);
    }

    public async Task<StudentInsightsDto> StudentAsync(Guid teacherId, Guid userId, CancellationToken ct)
    {
        var member = await (from m in db.GroupMembers
                            join g in db.Groups on m.GroupId equals g.Id
                            join u in db.Users on m.UserId equals u.Id
                            where m.UserId == userId && m.LeftAt == null && g.TeacherUserId == teacherId && g.ClosedAt == null && u.DeletedAt == null
                            orderby m.JoinedAt
                            select new { Member = new Member(u.Id, u.DisplayName, m.JoinedAt), m.GroupId }).ToListAsync(ct);
        if (member.Count == 0)
        {
            throw new NotFoundException("O‘quvchi topilmadi.");
        }

        var now = time.GetUtcNow();
        var today = KoreaTime.Day(now);
        var weeks = WeekStarts(today);
        List<Guid> ids = [userId];
        var days = await ActivityAsync(ids, weeks[0], ct);
        var reviewed = await ReviewedByWeekAsync(ids, weeks, now, ct);
        var tests = await UnitTestsAsync(ids, ct);
        var due = await TeacherService.DueOfAsync(db, ids, now, ct);
        var lastActive = await LastActiveAsync(ids, ct);
        var groups = member.Select(m => m.GroupId).ToList();
        var decks = await db.GroupDecks.AsNoTracking()
            .Where(d => groups.Contains(d.GroupId) && d.TeacherUserId == teacherId && d.Status == GroupDeckStatus.Published && d.DueAt != null).ToListAsync(ct);
        var late = await LateHomeworkAsync(decks, ids, now, ct);

        var weekPoints = weeks.Select((start, i) => new WeekPointDto(start,
            reviewed.Where(r => r.Week == i).Sum(r => r.Count), days.Count(d => WeekOf(weeks, d.Day) == i))).ToList();
        return new StudentInsightsDto(weekPoints, tests.Select(t => new TestPointDto(t.At, t.Percent, t.Unit)).ToList(),
            Flags(member[0].Member, today, lastActive, tests, due.GetValueOrDefault(userId), late[userId]), Options.LowScorePercent);
    }

    /// <summary>The risk flags of one learner, most serious first (the order of <see cref="RiskKind"/>).</summary>
    private List<RiskFlagDto> Flags(Member m, DateOnly today, Dictionary<Guid, DateOnly> lastActive, List<TestRow> tests, int due, IEnumerable<string> late)
    {
        var risk = Options.Risk;
        var flags = new List<RiskFlagDto>();
        // Never active since joining counts from the day they joined.
        var joined = KoreaTime.Day(m.JoinedAt);
        var last = lastActive.TryGetValue(m.Id, out var day) && day > joined ? day : joined;
        var away = today.DayNumber - last.DayNumber;
        if (away >= risk.InactiveDays)
        {
            flags.Add(new(RiskKind.Inactive, $"{away} kun kirmadi"));
        }

        if (tests.Count >= 2 && tests[^2].Percent - tests[^1].Percent >= risk.ScoreDropPoints)
        {
            flags.Add(new(RiskKind.ScoreDrop, $"Test {tests[^2].Percent}% → {tests[^1].Percent}%"));
        }

        flags.AddRange(late.Select(name => new RiskFlagDto(RiskKind.HomeworkLate, $"«{name}» vazifasi kechikdi")));
        if (due >= risk.DueBacklog)
        {
            flags.Add(new(RiskKind.DueBacklog, $"{due} karta kutmoqda"));
        }

        return flags;
    }

    /// <summary>For each learner, the decks whose due time passed in the last <c>HomeworkLateDays</c> days and that they had not done by it.</summary>
    private async Task<ILookup<Guid, string>> LateHomeworkAsync(List<GroupDeck> decks, List<Guid> ids, DateTimeOffset now, CancellationToken ct)
    {
        var from = now.AddDays(-Options.Risk.HomeworkLateDays);
        var late = new List<(Guid UserId, string Deck)>();
        foreach (var deck in decks.Where(d => d.DueAt <= now && d.DueAt > from).OrderBy(d => d.DueAt))
        {
            var done = await GroupDeckReport.DoneAsync(db, deck.Id, ids, deck.DueAt!.Value, ct);
            late.AddRange(ids.Where(id => !(done.TryGetValue(id, out var d) && d.Done)).Select(id => (id, deck.Name)));
        }

        return late.ToLookup(x => x.UserId, x => x.Deck);
    }

    /// <summary>Mondays of the last 8 weeks (Korea time), oldest first; the last is this week's.</summary>
    public static IReadOnlyList<DateOnly> WeekStarts(DateOnly today)
    {
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        return Enumerable.Range(0, Weeks).Select(i => monday.AddDays(-7 * (Weeks - 1 - i))).ToList();
    }

    private static int WeekOf(IReadOnlyList<DateOnly> weeks, DateOnly day)
    {
        for (var i = weeks.Count - 1; i >= 0; i--)
        {
            if (day >= weeks[i])
            {
                return i;
            }
        }

        return -1;
    }

    private async Task<List<ActivityRow>> ActivityAsync(List<Guid> ids, DateOnly from, CancellationToken ct) =>
        await db.ActivityDays.Where(a => ids.Contains(a.UserId) && a.Day >= from).Select(a => new ActivityRow(a.UserId, a.Day)).ToListAsync(ct);

    private async Task<Dictionary<Guid, DateOnly>> LastActiveAsync(List<Guid> ids, CancellationToken ct) =>
        await db.ActivityDays.Where(a => ids.Contains(a.UserId)).GroupBy(a => a.UserId)
            .Select(g => new { g.Key, Day = g.Max(a => a.Day) }).ToDictionaryAsync(x => x.Key, x => x.Day, ct);

    /// <summary>
    /// Cards answered per learner and week, each card once a week (L-61: "Eslay olmadim" counts, a technical error or an
    /// undone answer does not). One grouped query: the week is a CASE over the 8 Monday 00:00 (Korea) boundaries.
    /// </summary>
    private async Task<List<WeekCount>> ReviewedByWeekAsync(List<Guid> ids, IReadOnlyList<DateOnly> weeks, DateTimeOffset now, CancellationToken ct)
    {
        var starts = weeks.Select(KoreaTime.StartOf).ToList();
        var from = starts[0];
        var e = Expression.Parameter(typeof(ReviewEvent), "e");
        var at = Expression.Property(e, nameof(ReviewEvent.OccurredAt));
        Expression week = Expression.Constant(0);
        for (var i = 1; i < starts.Count; i++)
        {
            week = Expression.Condition(Expression.GreaterThanOrEqual(at, Expression.Constant(starts[i])), Expression.Constant(i), week);
        }

        var row = Expression.Lambda<Func<ReviewEvent, ReviewWeek>>(Expression.MemberInit(Expression.New(typeof(ReviewWeek)),
            Expression.Bind(typeof(ReviewWeek).GetProperty(nameof(ReviewWeek.OwnerId))!, Expression.Property(e, nameof(ReviewEvent.OwnerId))),
            Expression.Bind(typeof(ReviewWeek).GetProperty(nameof(ReviewWeek.CardId))!, Expression.Property(e, nameof(ReviewEvent.CardId))),
            Expression.Bind(typeof(ReviewWeek).GetProperty(nameof(ReviewWeek.Week))!, week)), e);

        return await db.ReviewEvents
            .Where(r => ids.Contains(r.OwnerId) && r.UndoneAt == null && r.Answer != ReviewAnswer.TechnicalError && r.OccurredAt >= from && r.OccurredAt <= now)
            .Select(row)
            .Distinct()
            .GroupBy(r => new { r.OwnerId, r.Week })
            .Select(g => new WeekCount(g.Key.OwnerId, g.Key.Week, g.Count()))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Finished unit tests (final and "Bilaman") per learner, oldest first, with their score. One query; the answers are
    /// read only to count the right ones and are not returned.
    /// </summary>
    private async Task<List<TestRow>> UnitTestsAsync(List<Guid> ids, CancellationToken ct)
    {
        var runs = await (from r in db.TestRuns.AsNoTracking()
                          join u in db.Units on r.UnitId equals u.Id into unit
                          from u in unit.DefaultIfEmpty()
                          where ids.Contains(r.OwnerId) && r.Status == TestRunStatus.Finished && r.Purpose != TestPurpose.Practice && r.FinishedAt != null
                          select new { r.Id, r.OwnerId, r.FinishedAt, r.Questions, r.Answers, Level = (Domain.Catalog.CatalogLevel?)u.Level, Number = (int?)u.Number })
            .ToListAsync(ct);
        return runs
            .Select(r => new TestRow(r.OwnerId, r.FinishedAt!.Value, Catalog.UnitTestService.ScorePercent(r.Questions.Count, r.Answers.Count(a => a.IsCorrect)),
                r.Level is { } level ? Study.LearnerItems.Label(level, r.Number!.Value) : null, r.Id))
            .OrderBy(r => r.At).ThenBy(r => r.Id)
            .ToList();
    }

    private sealed record Member(Guid Id, string? Name, DateTimeOffset JoinedAt);

    private sealed record ActivityRow(Guid UserId, DateOnly Day);

    private sealed record WeekCount(Guid OwnerId, int Week, int Count);

    private sealed record TestRow(Guid OwnerId, DateTimeOffset At, int Percent, string? Unit, Guid Id);

    private sealed class ReviewWeek
    {
        public Guid OwnerId { get; set; }

        public Guid CardId { get; set; }

        public int Week { get; set; }
    }
}
