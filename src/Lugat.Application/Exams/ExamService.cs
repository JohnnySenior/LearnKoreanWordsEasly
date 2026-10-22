using Lugat.Application.Abstractions;
using Lugat.Application.Grammar;
using Lugat.Contracts;
using Lugat.Domain.Exams;
using Lugat.Domain.Grammar;
using Lugat.Domain.Learning;
using Lugat.Domain.Testing;
using Microsoft.EntityFrameworkCore;

namespace Lugat.Application.Exams;

/// <summary>The learner's TOPIK date, the countdown and what is left to cover before it (D-34).</summary>
public sealed class ExamService(IAppDbContext db, GrammarStudyService grammar, TimeProvider time)
{
    /// <summary>Two years is far beyond any planned TOPIK sitting; a later date is almost certainly a typo.</summary>
    public const int MaxYearsAhead = 2;

    public async Task<ExamDto> GetAsync(Guid ownerId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == ownerId, ct);
        var zone = user.Zone();
        var now = time.GetUtcNow();
        var localNow = TimeZoneInfo.ConvertTime(now, zone);
        var today = DateOnly.FromDateTime(localNow.DateTime);

        // A word counts as started once any of its two cards has left "new".
        var cards = await (
                from c in db.Cards
                join s in db.WordSenses on c.WordSenseId equals s.Id
                where c.OwnerId == ownerId && s.IsActive
                select new { c.WordSenseId, c.Phase })
            .AsNoTracking()
            .ToListAsync(ct);
        var newWords = cards.GroupBy(c => c.WordSenseId).Count(g => g.All(c => c.Phase == LearningPhase.New));

        var startOfDay = new DateTimeOffset(localNow.Date, localNow.Offset).ToUniversalTime(); // Npgsql takes UTC only
        var todayEvents = await db.ReviewEvents
            .Where(e => e.OwnerId == ownerId && e.UndoneAt == null && e.OccurredAt >= startOfDay)
            .Select(e => new { e.WordSenseId, e.PreviousState })
            .AsNoTracking()
            .ToListAsync(ct);
        var startedToday = todayEvents.Where(e => e.PreviousState.Phase == LearningPhase.New).Select(e => e.WordSenseId).Distinct().Count();

        var grammarLeft = (await grammar.ListAsync(ownerId, ct))
            .Count(i => i.Status == GrammarItemStatus.Approved && i.Mastery != GrammarMastery.Independent);

        var mocks = await db.TestRuns
            .Where(r => r.OwnerId == ownerId && r.BookTestId != null && r.TimeLimitMinutes != null && r.Status == TestRunStatus.Finished)
            .OrderByDescending(r => r.FinishedAt)
            .Take(20)
            .AsNoTracking()
            .ToListAsync(ct);
        var weekAgo = now.AddDays(-7);
        var last = mocks.FirstOrDefault();
        var bookTests = await db.BookTests.CountAsync(t => t.OwnerId == ownerId && t.Status == BookTestStatus.Ready, ct);

        var plan = ExamPlanner.Plan(today, user.ExamDate, newWords, grammarLeft);
        return new ExamDto(
            user.ExamDate,
            user.TargetLevel,
            plan.Phase,
            plan.DaysLeft,
            plan.StudyDays,
            newWords,
            plan.WordsPerDay,
            startedToday,
            grammarLeft,
            plan.GrammarPerWeek,
            plan.MocksPerWeek,
            mocks.Count(r => r.FinishedAt >= weekAgo),
            bookTests,
            last is null ? null : last.Answers.Count(a => a.IsCorrect) * 100 / Math.Max(1, last.Questions.Count),
            last?.FinishedAt);
    }

    public async Task<ExamDto> UpdateAsync(Guid ownerId, UpdateExamRequest request, CancellationToken ct)
    {
        var user = await db.Users.SingleAsync(u => u.Id == ownerId, ct);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), user.Zone()).DateTime);
        if (request.ExamDate is { } date && (date < today || date > today.AddYears(MaxYearsAhead)))
        {
            throw new ValidationException($"Imtihon sanasini bugundan {MaxYearsAhead} yil ichida tanlang.");
        }

        if (request.TargetLevel is { } level && level is < 3 or > 6)
        {
            throw new ValidationException("Maqsad darajasi 3 dan 6 gacha bo‘lsin (TOPIK II).");
        }

        user.ExamDate = request.ExamDate;
        user.TargetLevel = request.TargetLevel;
        await db.SaveChangesAsync(ct);
        return await GetAsync(ownerId, ct);
    }

    /// <summary>Days to the exam in the learner's zone, or null without a future date. Used by reminders.</summary>
    public static int? DaysLeft(Domain.Users.User user, DateTimeOffset now)
    {
        if (user.ExamDate is not { } date)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, user.Zone()).DateTime);
        var days = date.DayNumber - today.DayNumber;
        return days >= 0 ? days : null;
    }
}
