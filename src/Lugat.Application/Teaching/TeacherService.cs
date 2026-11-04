using System.Security.Cryptography;
using Lugat.Application.Abstractions;
using Lugat.Application.Mistakes;
using Lugat.Application.Study;
using Lugat.Contracts;
using Lugat.Domain;
using Lugat.Domain.Catalog;
using Lugat.Domain.Learning;
using Lugat.Domain.Premium;
using Lugat.Domain.Teaching;
using Lugat.Domain.Testing;
using Microsoft.EntityFrameworkCore;

namespace Lugat.Application.Teaching;

/// <summary>
/// The teacher panel (spec §9.2, §9.3). A teacher sees only learners who joined one of their open groups and have
/// not left; anyone else is 404, as if they did not exist (spec §6.4, §13.8). What is shown is limited to unit
/// results, activity and most-missed words: no answer history, payments or logins.
/// </summary>
public sealed class TeacherService(IAppDbContext db, TimeProvider time, MistakeService mistakes)
{
    public const int MaxGroupName = 100;

    public const int TopWords = 10;

    private const string CodeAlphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ"; // no 0/O, 1/I/L

    public async Task<IReadOnlyList<TeacherGroupDto>> GroupsAsync(Guid teacherId, CancellationToken ct) =>
        await db.Groups.Where(g => g.TeacherUserId == teacherId && g.ClosedAt == null).OrderBy(g => g.CreatedAt)
            .Select(g => new TeacherGroupDto(g.Id, g.Name, g.InviteCode, db.GroupMembers.Count(m => m.GroupId == g.Id && m.LeftAt == null), g.CreatedAt))
            .ToListAsync(ct);

    /// <summary>Creates a group with a fresh invite code; the same <c>Id</c> again returns the same group.</summary>
    public async Task<TeacherGroupDto> CreateGroupAsync(Guid teacherId, CreateGroupRequest request, CancellationToken ct)
    {
        var name = (request.Name ?? "").Trim();
        if (name.Length is 0 or > MaxGroupName)
        {
            throw new ValidationException($"Guruh nomi 1 dan {MaxGroupName} belgigacha bo‘lsin.");
        }

        if (await db.Groups.SingleOrDefaultAsync(g => g.Id == request.Id, ct) is { } existing)
        {
            return existing.TeacherUserId == teacherId ? await GroupDtoAsync(existing.Id, ct) : throw new NotFoundException("Guruh topilmadi.");
        }

        var now = time.GetUtcNow();
        var group = new Group { Id = request.Id, TeacherUserId = teacherId, Name = name, InviteCode = NewInviteCode(), CreatedAt = now };
        db.Groups.Add(group);
        await SaveWithFreshCodeAsync(group, creating: true, ct);
        return await GroupDtoAsync(request.Id, ct);
    }

    /// <summary>A new invite link; the old one stops working at once (e.g. it was shared too widely).</summary>
    public async Task<TeacherGroupDto> NewInviteAsync(Guid teacherId, Guid groupId, CancellationToken ct)
    {
        var group = await OwnGroupAsync(teacherId, groupId, ct);
        group.InviteCode = NewInviteCode();
        await SaveWithFreshCodeAsync(group, creating: false, ct);
        return await GroupDtoAsync(groupId, ct);
    }

    /// <summary>Closes the group: the teacher no longer sees its learners and the link stops working.</summary>
    public async Task CloseGroupAsync(Guid teacherId, Guid groupId, CancellationToken ct)
    {
        var group = await OwnGroupAsync(teacherId, groupId, ct);
        group.ClosedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    /// <summary>The group's learners table and its 10 hardest words (spec §9.2).</summary>
    public async Task<GroupDetailDto> GroupAsync(Guid teacherId, Guid groupId, CancellationToken ct)
    {
        var group = await OwnGroupAsync(teacherId, groupId, ct);
        var members = await (from m in db.GroupMembers
                             join u in db.Users on m.UserId equals u.Id
                             where m.GroupId == groupId && m.LeftAt == null && u.DeletedAt == null
                             select new { u.Id, u.DisplayName, u.LastSeenAt, m.JoinedAt }).ToListAsync(ct);
        var ids = members.Select(m => m.Id).ToList();
        var units = await UnitsOfAsync(ids, ct);

        var today = KoreaTime.Day(time.GetUtcNow());
        var weekStart = today.AddDays(-6);
        var active = (await db.ActivityDays.Where(a => ids.Contains(a.UserId) && a.Day >= weekStart).ToListAsync(ct))
            .GroupBy(a => a.UserId).ToDictionary(g => g.Key, g => g.Count());

        // The latest unit test of each learner, found in the database; only those runs are read (audit 2026-10-26).
        var finished = db.TestRuns.Where(r => ids.Contains(r.OwnerId) && r.Status == TestRunStatus.Finished && r.Purpose != TestPurpose.Practice);
        var latestIds = await finished.GroupBy(r => r.OwnerId)
            .Select(g => g.OrderByDescending(r => r.FinishedAt).ThenByDescending(r => r.Id).Select(r => r.Id).First())
            .ToListAsync(ct);
        var lastTests = (await db.TestRuns.AsNoTracking().Where(r => latestIds.Contains(r.Id))
                .Select(r => new { r.OwnerId, r.Questions, r.Answers }).ToListAsync(ct))
            .ToDictionary(r => r.OwnerId, r => Catalog.UnitTestService.ScorePercent(r.Questions.Count, r.Answers.Count(a => a.IsCorrect)));

        var (reviewed, due) = await ReviewsOfAsync(ids, ct);
        var students = members
            .Select(m => new GroupStudentDto(m.Id, m.DisplayName, CurrentUnit(units[m.Id]), units[m.Id].Count(u => u.Status == UnitProgressStatus.Passed),
                lastTests.TryGetValue(m.Id, out var score) ? score : null, active.GetValueOrDefault(m.Id), m.LastSeenAt, m.JoinedAt,
                reviewed.GetValueOrDefault(m.Id), due.GetValueOrDefault(m.Id)))
            .OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var missed = (await mistakes.WordsOfAsync(ids, ct)).SelectMany(p => p.Value.Select(w => (Word: w, Learner: p.Key))).ToList();

        var hardest = missed.GroupBy(x => x.Word.LearningItemId)
            .Select(g => new MissedWordDto(g.First().Word.Korean, g.First().Word.Uzbek, g.First().Word.Unit, g.Sum(x => x.Word.Mistakes), g.Select(x => x.Learner).Distinct().Count(),
                g.SelectMany(x => x.Word.Sources).Distinct().ToList()))
            .OrderByDescending(w => w.Mistakes).ThenByDescending(w => w.Learners).ThenBy(w => w.Korean, StringComparer.Ordinal)
            .Take(TopWords)
            .ToList();
        return new GroupDetailDto(group.Id, group.Name, group.InviteCode, students, hardest);
    }

    /// <summary>One learner: scores by unit and the 10 most-missed words (spec §9.2). 404 unless in the teacher's group.</summary>
    public async Task<StudentDetailDto> StudentAsync(Guid teacherId, Guid userId, CancellationToken ct)
    {
        var visible = await (from m in db.GroupMembers
                             join g in db.Groups on m.GroupId equals g.Id
                             join u in db.Users on m.UserId equals u.Id
                             where m.UserId == userId && m.LeftAt == null && g.TeacherUserId == teacherId && g.ClosedAt == null && u.DeletedAt == null
                             select u.DisplayName).ToListAsync(ct);
        if (visible.Count == 0)
        {
            throw new NotFoundException("O‘quvchi topilmadi.");
        }

        var units = (await UnitsOfAsync([userId], ct))[userId];
        var words = (await mistakes.GetAsync(userId, ct)).Words.Take(TopWords)
            .Select(w => new MissedWordDto(w.Korean, w.Uzbek, w.Unit, w.Mistakes, 1, w.Sources)).ToList();
        var (reviewed, due) = await ReviewsOfAsync([userId], ct);
        var week = new StudentWeekDto(reviewed.GetValueOrDefault(userId), due.GetValueOrDefault(userId), await PracticeOfAsync(userId, ct));
        return new StudentDetailDto(userId, visible[0], CurrentUnit(units),
            units.Select(u => new StudentUnitDto(u.Label, u.Status, u.Best, u.Attempts, u.PassedBy)).ToList(), words, week);
    }

    public const int WeekDays = 7;

    /// <summary>
    /// For each learner (L-61): cards answered in the last 7 days, each card once ("Eslay olmadim" counts, a technical
    /// error or an undone answer does not), and cards waiting for review now, as the learner's own Today page counts
    /// them. Two queries whatever the size of the group.
    /// </summary>
    private async Task<(Dictionary<Guid, int> Reviewed, Dictionary<Guid, int> Due)> ReviewsOfAsync(List<Guid> ids, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var since = now.AddDays(-WeekDays);
        var reviewed = await db.ReviewEvents
            .Where(e => ids.Contains(e.OwnerId) && e.UndoneAt == null && e.OccurredAt >= since && e.Answer != ReviewAnswer.TechnicalError)
            .GroupBy(e => e.OwnerId)
            .Select(g => new { g.Key, Count = g.Select(e => e.CardId).Distinct().Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var due = await (from c in db.Cards
                         join i in db.LearningItems on c.LearningItemId equals i.Id
                         where ids.Contains(c.OwnerId) && i.IsActive && c.Phase != LearningPhase.New && c.DueAt <= now
                         group c by c.OwnerId into g
                         select new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return (reviewed, due);
    }

    /// <summary>What the learner practised in the last 7 days (L-61), in the Mashq page's order; nothing about the answers.</summary>
    private async Task<IReadOnlyList<PracticeCountDto>> PracticeOfAsync(Guid userId, CancellationToken ct)
    {
        var since = time.GetUtcNow().AddDays(-WeekDays);
        var practice = new List<PracticeCountDto>
        {
            new("Yozma mashq", await db.WritingAttempts.CountAsync(w => w.OwnerId == userId && w.CreatedAt >= since, ct), Words: true),
            new("Sinov (mashq)", await db.TestRuns.CountAsync(r => r.OwnerId == userId && r.Purpose == TestPurpose.Practice
                && r.Status == TestRunStatus.Finished && r.FinishedAt >= since, ct)),
            new("Krossvord", await db.Crosswords.CountAsync(x => x.OwnerId == userId && x.CreatedAt >= since, ct)),
            new("Gap zinapoyasi", await db.SentenceLadders.CountAsync(l => l.OwnerId == userId && l.CreatedAt >= since, ct)),
            new("O‘z gapingiz", await db.SentenceChecks.CountAsync(c => c.OwnerId == userId && c.CreatedAt >= since, ct)),
            new("Tinglash", await db.ListeningSets.CountAsync(l => l.OwnerId == userId && l.CreatedAt >= since, ct)),
        };
        return practice.Where(p => p.Count > 0).ToList();
    }

    /// <summary>
    /// Payments made with the teacher's codes, by month (spec §9.3): counts and sums only, never who paid. Refunded
    /// payments do not count.
    /// </summary>
    public async Task<CommissionReportDto> CommissionAsync(Guid teacherId, CancellationToken ct)
    {
        var codes = await db.PromoCodes.Where(p => p.TeacherUserId == teacherId).OrderBy(p => p.CreatedAt).ToListAsync(ct);
        var ids = codes.Select(c => (Guid?)c.Id).ToList();
        var payments = await db.Payments.Where(p => ids.Contains(p.PromoCodeId) && p.Status == PaymentStatus.Confirmed)
            .Select(p => new { p.PaidAt, p.Amount, p.Commission, p.Currency }).ToListAsync(ct);
        var payouts = await db.CommissionPayouts.Where(p => p.TeacherUserId == teacherId).ToDictionaryAsync(p => (p.Month, p.Currency), ct);
        // Won and so‘m stay apart (L-48): a month paid in both has a row for each.
        var months = payments.GroupBy(p => (Month: KoreaTime.MonthOf(p.PaidAt), p.Currency))
            .OrderByDescending(g => g.Key.Month).ThenBy(g => g.Key.Currency)
            .Select(g => new CommissionMonthDto(g.Key.Month, g.Count(), g.Sum(p => p.Amount), g.Sum(p => p.Commission),
                payouts.ContainsKey(g.Key), payouts.GetValueOrDefault(g.Key)?.PaidAt, g.Key.Currency))
            .ToList();
        var totals = payments.GroupBy(p => p.Currency).OrderBy(g => g.Key)
            .Select(g => new MoneyTotalDto(g.Key, g.Sum(p => p.Amount), g.Sum(p => p.Commission))).ToList();
        return new CommissionReportDto(
            codes.Select(c => new TeacherPromoDto(c.Code, c.DiscountPercent, c.CommissionPercent, c.IsActive)).ToList(),
            months, payments.Count, totals);
    }

    private sealed record UnitRow(Guid UserId, CatalogLevel Level, int Number, UnitProgressStatus Status, int? Best, int Attempts, UnitPassedBy? PassedBy)
    {
        public string Label => LearnerItems.Label(Level, Number);
    }

    private async Task<ILookup<Guid, UnitRow>> UnitsOfAsync(List<Guid> userIds, CancellationToken ct) =>
        (await (from p in db.UnitProgress
                join u in db.Units on p.UnitId equals u.Id
                where userIds.Contains(p.UserId)
                select new UnitRow(p.UserId, u.Level, u.Number, p.Status, p.BestScorePercent, p.Attempts, p.PassedBy)).ToListAsync(ct))
            .OrderBy(u => u.Level).ThenBy(u => u.Number)
            .ToLookup(u => u.UserId);

    /// <summary>The furthest unit still open; when every opened unit is passed, the last one passed.</summary>
    private static string? CurrentUnit(IEnumerable<UnitRow> units)
    {
        var list = units.ToList();
        return (list.LastOrDefault(u => u.Status == UnitProgressStatus.Open) ?? list.LastOrDefault())?.Label;
    }

    private async Task<Group> OwnGroupAsync(Guid teacherId, Guid groupId, CancellationToken ct) =>
        await db.Groups.SingleOrDefaultAsync(g => g.Id == groupId && g.TeacherUserId == teacherId && g.ClosedAt == null, ct)
            ?? throw new NotFoundException("Guruh topilmadi.");

    private async Task<TeacherGroupDto> GroupDtoAsync(Guid groupId, CancellationToken ct) =>
        await db.Groups.Where(g => g.Id == groupId)
            .Select(g => new TeacherGroupDto(g.Id, g.Name, g.InviteCode, db.GroupMembers.Count(m => m.GroupId == g.Id && m.LeftAt == null), g.CreatedAt))
            .SingleAsync(ct);

    /// <summary>Saves; on the rare clash of a new invite code tries another. A clash on a new group's id is a double tap.</summary>
    private async Task SaveWithFreshCodeAsync(Group group, bool creating, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException) when (attempt < 5)
            {
                if (creating && await db.Groups.AsNoTracking().AnyAsync(g => g.Id == group.Id, ct))
                {
                    db.DiscardChanges();
                    return;
                }

                group.InviteCode = NewInviteCode();
            }
        }
    }

    private static string NewInviteCode() => RandomNumberGenerator.GetString(CodeAlphabet, 8);
}
