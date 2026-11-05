using Lugat.Application.Abstractions;
using Lugat.Application.Ai;
using Lugat.Application.Study;
using Lugat.Application.Teaching;
using Lugat.Contracts;
using Lugat.Domain;
using Lugat.Domain.Catalog;
using Lugat.Domain.Operations;
using Lugat.Domain.Premium;
using Lugat.Domain.Teaching;
using Lugat.Domain.Testing;
using Lugat.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Lugat.Application.Admin;

/// <summary>
/// The owner's admin panel (spec §10), besides content (<c>CatalogAdminService</c>) and payments
/// (<c>PremiumService</c>): users and roles, Premium changed by hand with a reason, promo codes on/off, teachers'
/// commissions marked paid, spending and the key figures. Every hand change is written to the audit log.
/// </summary>
public sealed class AdminService(IAppDbContext db, TimeProvider time, IOperationLock operations, AiUsageService usage)
{
    public const int MaxAdjustDays = 3650;

    private static readonly UserRoles[] Assignable = [UserRoles.Teacher, UserRoles.Editor, UserRoles.Admin];

    public async Task<AdminUserDetailDto> UserAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null, ct)
            ?? throw new NotFoundException("Foydalanuvchi topilmadi.");
        var now = time.GetUtcNow();
        var logins = await db.UserLogins.Where(l => l.UserId == userId).OrderBy(l => l.CreatedAt)
            .Select(l => l.Display == null ? l.Provider.ToString() : l.Provider + " " + l.Display).ToListAsync(ct);
        var subs = await db.Subscriptions.Where(s => s.UserId == userId).OrderBy(s => s.StartsAt)
            .Select(s => new SubscriptionDto(s.Id, s.Plan, s.Source, s.StartsAt, s.EndsAt)).ToListAsync(ct);
        var running = subs.Where(s => s.EndsAt > now).ToList();
        var premium = running.Any(s => s.StartsAt <= now);
        var units = (await (from p in db.UnitProgress
                            join u in db.Units on p.UnitId equals u.Id
                            where p.UserId == userId
                            select new { u.Level, u.Number, p.Status, p.BestScorePercent, p.Attempts, p.PassedBy }).ToListAsync(ct))
            .OrderBy(u => u.Level).ThenBy(u => u.Number)
            .Select(u => new StudentUnitDto(LearnerItems.Label(u.Level, u.Number), u.Status, u.BestScorePercent, u.Attempts, u.PassedBy)).ToList();
        var groups = await (from m in db.GroupMembers
                            join g in db.Groups on m.GroupId equals g.Id
                            where m.UserId == userId && m.LeftAt == null && g.ClosedAt == null
                            select g.Name + " — " + (db.Users.Where(t => t.Id == g.TeacherUserId).Select(t => t.DisplayName).FirstOrDefault() ?? "—"))
            .ToListAsync(ct);
        var audit = await (from a in db.AdminAudit
                           where a.TargetUserId == userId
                           orderby a.At descending
                           select new AuditEntryDto(a.At, a.Action, a.Detail, db.Users.Where(x => x.Id == a.AdminUserId).Select(x => x.DisplayName).FirstOrDefault()))
            .Take(20).ToListAsync(ct);
        return new AdminUserDetailDto(user.Id, user.DisplayName, user.PaymentCode, RoleNames(user.Roles), logins, user.CreatedAt, user.LastSeenAt,
            premium, premium ? running.Max(s => s.EndsAt) : null, await GroupService.AttachedPromoAsync(db, userId, now, ct), subs, units, groups, audit);
    }

    /// <summary>Gives or takes Teacher, Editor and Admin (spec §10). An admin cannot take Admin away from themselves.</summary>
    public async Task<AdminUserDetailDto> SetRolesAsync(Guid adminId, Guid userId, SetRolesRequest request, CancellationToken ct)
    {
        var roles = UserRoles.Learner;
        foreach (var name in request.Roles ?? [])
        {
            roles |= Enum.TryParse<UserRoles>(name, ignoreCase: true, out var role) && Assignable.Contains(role)
                ? role
                : throw new ValidationException($"Bunday rol yo‘q: {name}.");
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null, ct) ?? throw new NotFoundException("Foydalanuvchi topilmadi.");
        if (userId == adminId && !roles.HasFlag(UserRoles.Admin))
        {
            throw new ValidationException("O‘zingizdan admin rolini olib bo‘lmaydi: boshqa admin buni qilsin.");
        }

        if (user.Roles != roles)
        {
            Audit(adminId, "roles", userId, $"Rollar: {RoleLabels(user.Roles)} → {RoleLabels(roles)}");
            user.Roles = roles;
            await db.SaveChangesAsync(ct);
        }

        return await UserAsync(userId, ct);
    }

    /// <summary>
    /// Premium changed by hand, always with a reason (spec §10). Days given are a gift period after the current one;
    /// days taken come off the end of the learner's Premium (the latest periods first), never before now.
    /// </summary>
    public async Task<AdminUserDetailDto> AdjustPremiumAsync(Guid adminId, Guid userId, AdjustPremiumRequest request, CancellationToken ct)
    {
        var reason = (request.Reason ?? "").Trim();
        if (reason.Length < 3 || reason.Length > 300)
        {
            throw new ValidationException("Sababni yozing (3–300 belgi).");
        }

        if (request.Days == 0 || Math.Abs(request.Days) > MaxAdjustDays)
        {
            throw new ValidationException($"Kunlar soni 1 dan {MaxAdjustDays} gacha (olib tashlash uchun minus bilan).");
        }

        await using var lease = await operations.AcquireAsync("premium:" + userId, ct);
        if (!await db.Users.AnyAsync(u => u.Id == userId && u.DeletedAt == null, ct))
        {
            throw new NotFoundException("Foydalanuvchi topilmadi.");
        }

        var now = time.GetUtcNow();
        var running = await db.Subscriptions.Where(s => s.UserId == userId && s.EndsAt > now).OrderByDescending(s => s.EndsAt).ToListAsync(ct);
        if (request.Days > 0)
        {
            var start = PremiumMath.NextStart(now, running.Select(s => s.EndsAt));
            db.Subscriptions.Add(new Subscription
            {
                Id = Guid.CreateVersion7(now),
                UserId = userId,
                Plan = SubscriptionPlan.Gift,
                StartsAt = start,
                EndsAt = start.AddDays(request.Days),
                Source = SubscriptionSource.Admin,
                CreatedByUserId = adminId,
                CreatedAt = now,
            });
        }
        else
        {
            if (running.Count == 0)
            {
                throw new ValidationException("Bu foydalanuvchida Premium yo‘q: olib tashlanadigan kun yo‘q.");
            }

            var cut = TimeSpan.FromDays(-request.Days);
            foreach (var s in running)
            {
                var floor = s.StartsAt > now ? s.StartsAt : now;
                var take = s.EndsAt - floor < cut ? s.EndsAt - floor : cut;
                s.EndsAt -= take;
                cut -= take;
                if (s.EndsAt <= floor)
                {
                    (s.StartsAt, s.EndsAt) = (now, now); // used up: no longer running, so a later payment starts at the right place
                }

                if (cut <= TimeSpan.Zero)
                {
                    break;
                }
            }
        }

        Audit(adminId, "premium", userId, $"{(request.Days > 0 ? "+" : "")}{request.Days} kun: {reason}");
        await db.SaveChangesAsync(ct);
        return await UserAsync(userId, ct);
    }

    /// <summary>A promo code is never deleted (payments point to it); it is turned off and on.</summary>
    public async Task SetPromoActiveAsync(Guid adminId, Guid promoId, bool active, CancellationToken ct)
    {
        var promo = await db.PromoCodes.SingleOrDefaultAsync(p => p.Id == promoId, ct) ?? throw new NotFoundException("Promo-kod topilmadi.");
        if (promo.IsActive != active)
        {
            promo.IsActive = active;
            Audit(adminId, "promo", promo.TeacherUserId, $"{promo.Code}: {(active ? "yoqildi" : "o‘chirildi")}");
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Every teacher's commission by month (spec §9.3), newest first; refunded payments do not count.</summary>
    public async Task<IReadOnlyList<TeacherCommissionDto>> CommissionsAsync(CancellationToken ct)
    {
        var rows = await (from p in db.Payments
                          join c in db.PromoCodes on p.PromoCodeId equals c.Id
                          where p.Status == PaymentStatus.Confirmed && c.TeacherUserId != null
                          select new { Teacher = c.TeacherUserId!.Value, p.PaidAt, p.Amount, p.Commission, p.Currency }).ToListAsync(ct);
        var teacherIds = rows.Select(r => r.Teacher).Distinct().ToList();
        var names = await db.Users.Where(u => teacherIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        var payouts = await db.CommissionPayouts.ToDictionaryAsync(p => (p.TeacherUserId, p.Month, p.Currency), ct);
        // One row per teacher, month and currency: won and so‘m are never added together (L-48).
        return rows.GroupBy(r => (r.Teacher, Month: KoreaTime.MonthOf(r.PaidAt), r.Currency))
            .Select(g => new TeacherCommissionDto(g.Key.Teacher, names.GetValueOrDefault(g.Key.Teacher), g.Key.Month, g.Count(), g.Sum(r => r.Amount),
                g.Sum(r => r.Commission), payouts.ContainsKey(g.Key), payouts.GetValueOrDefault(g.Key)?.PaidAt, g.Key.Currency))
            .OrderByDescending(r => r.Month).ThenBy(r => r.Teacher, StringComparer.CurrentCultureIgnoreCase).ThenBy(r => r.Currency)
            .ToList();
    }

    /// <summary>The owner paid the month's commission outside the app (spec §9.3); marking it again changes nothing.</summary>
    public async Task<TeacherCommissionDto> MarkPaidAsync(Guid adminId, MarkCommissionPaidRequest request, CancellationToken ct)
    {
        var month = new DateOnly(request.Month.Year, request.Month.Month, 1);
        var due = (await CommissionsAsync(ct)).SingleOrDefault(c => c.TeacherUserId == request.TeacherUserId && c.Month == month && c.Currency == request.Currency)
            ?? throw new NotFoundException("Bu oy uchun komissiya yo‘q.");
        if (!due.Paid)
        {
            db.CommissionPayouts.Add(new CommissionPayout
            {
                Id = Guid.CreateVersion7(time.GetUtcNow()),
                TeacherUserId = request.TeacherUserId,
                Month = month,
                Amount = due.Commission,
                Currency = due.Currency,
                PaidAt = time.GetUtcNow(),
                MarkedByUserId = adminId,
            });
            Audit(adminId, "payout", request.TeacherUserId, $"{month:yyyy-MM}: {Money.Format(due.Commission, due.Currency)} to‘landi");
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                db.DiscardChanges(); // marked by a parallel click
            }
        }

        return (await CommissionsAsync(ct)).Single(c => c.TeacherUserId == request.TeacherUserId && c.Month == month && c.Currency == request.Currency);
    }

    /// <summary>AI and voice spending (spec §10 "Sarf") by Korea day and by feature, plus this month against the budget.</summary>
    public async Task<UsageReportDto> UsageAsync(int days, CancellationToken ct)
    {
        days = Math.Clamp(days, 1, 92);
        var since = KoreaTime.StartOf(KoreaTime.Day(time.GetUtcNow()).AddDays(1 - days));
        var rows = await db.AiUsages.Where(u => u.OccurredAt >= since)
            .Select(u => new { u.OccurredAt, u.Feature, u.Unit, u.InputTokens, u.OutputTokens, u.EstimatedUsd, u.Succeeded, u.Error })
            .ToListAsync(ct);
        var byDay = rows.GroupBy(r => KoreaTime.Day(r.OccurredAt)).OrderByDescending(g => g.Key)
            .Select(g => new UsageDayDto(g.Key, g.Count(), g.Count(r => !r.Succeeded && r.Error != "pending"), g.Sum(r => r.EstimatedUsd ?? 0))).ToList();
        var byFeature = rows.GroupBy(r => (r.Feature, r.Unit)).OrderByDescending(g => g.Sum(r => r.EstimatedUsd ?? 0)).ThenByDescending(g => g.Count())
            .Select(g => new UsageFeatureDto(g.Key.Feature, g.Key.Unit, g.Count(), g.Sum(r => (long)r.InputTokens + r.OutputTokens), g.Sum(r => r.EstimatedUsd ?? 0),
                g.Count(r => r.EstimatedUsd == null))).ToList();
        return new UsageReportDto(days, byDay, byFeature, await usage.BudgetAsync(ct));
    }

    /// <summary>
    /// The key figures (spec §10). "Active" means the user opened the app that Korea day (<c>UserActivityDay</c>).
    /// D1/D7: of the users who signed up at least 1 (7) days ago, within the last 30 days, the share active exactly
    /// 1 (7) days after their sign-up day.
    /// </summary>
    public async Task<MetricsDto> MetricsAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var today = KoreaTime.Day(now);
        var users = await db.Users.Where(u => u.DeletedAt == null).Select(u => new { u.Id, u.CreatedAt }).ToListAsync(ct);
        var signUps = users.ToDictionary(u => u.Id, u => KoreaTime.Day(u.CreatedAt));

        var from = today.AddDays(-44);
        var active = await db.ActivityDays.Where(a => a.Day >= from).ToListAsync(ct);
        var activeSet = active.Select(a => (a.UserId, a.Day)).ToHashSet();
        var daily = Enumerable.Range(0, 14).Select(i => today.AddDays(-i))
            .Select(d => new ActiveDayDto(d, active.Count(a => a.Day == d))).ToList();

        RetentionDto Retention(int after)
        {
            var cohort = signUps.Where(s => s.Value <= today.AddDays(-after) && s.Value >= today.AddDays(-30 - after)).ToList();
            var returned = cohort.Count(s => activeSet.Contains((s.Key, s.Value.AddDays(after))));
            return new RetentionDto(cohort.Count, returned, cohort.Count == 0 ? null : (int)Math.Round(returned * 100.0 / cohort.Count));
        }

        var premiumNow = await db.Subscriptions.Where(s => s.StartsAt <= now && s.EndsAt > now).Select(s => s.UserId).Distinct().CountAsync(ct);
        var revenueSince = KoreaTime.StartOf(KoreaTime.MonthOf(now).AddMonths(-5));
        var revenue = (await db.Payments.Where(p => p.Status == PaymentStatus.Confirmed && p.PaidAt >= revenueSince)
                .Select(p => new { p.PaidAt, p.Amount, p.Commission, p.Currency }).ToListAsync(ct))
            .GroupBy(p => (Month: KoreaTime.MonthOf(p.PaidAt), p.Currency)).OrderByDescending(g => g.Key.Month).ThenBy(g => g.Key.Currency)
            .Select(g => new RevenueMonthDto(g.Key.Month, g.Count(), g.Sum(p => p.Amount), g.Sum(p => p.Commission), g.Key.Currency)).ToList();

        var tests = await db.TestRuns.Where(r => r.Purpose == TestPurpose.UnitFinal && r.Status == TestRunStatus.Finished && r.UnitId != null)
            .GroupBy(r => r.UnitId)
            .Select(g => new { UnitId = g.Key!.Value, Attempts = g.Count(), Passed = g.Count(r => r.Passed == true) })
            .ToListAsync(ct);
        var labels = await LearnerItems.UnitLabelsAsync(db, tests.Select(t => (Guid?)t.UnitId), ct);
        var unitIds = labels.Keys.ToList();
        var unitOrder = await db.Units.Where(u => unitIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => ((int)u.Level * 10000) + u.Number, ct);
        var units = tests.OrderBy(t => unitOrder.GetValueOrDefault(t.UnitId))
            .Select(t => new UnitPassRateDto(labels.GetValueOrDefault(t.UnitId) ?? "?", t.Attempts, t.Passed, t.Attempts == 0 ? null : (int)Math.Round(t.Passed * 100.0 / t.Attempts)))
            .ToList();

        return new MetricsDto(users.Count, signUps.Count(s => s.Value > today.AddDays(-7)), signUps.Count(s => s.Value > today.AddDays(-30)),
            daily, Retention(1), Retention(7), premiumNow, revenue, units, await PilotAsync(today, ct),
            await ChannelsAsync(today, ct), await FirstUnitReturnAsync(today, ct));
    }

    /// <summary>L-63: learners active in the last 30 days by the reminder channel that reaches them now.</summary>
    private async Task<ReminderChannelsDto> ChannelsAsync(DateOnly today, CancellationToken ct)
    {
        var since = today.AddDays(-29);
        var rows = await (from u in db.Users
                          where u.DeletedAt == null && db.ActivityDays.Any(a => a.UserId == u.Id && a.Day >= since)
                          select new
                          {
                              u.RemindersEnabled,
                              Push = db.PushSubscriptions.Any(p => p.UserId == u.Id),
                              Telegram = db.UserLogins.Any(l => l.UserId == u.Id && l.Provider == LoginProvider.Telegram) && u.BotBlockedAt == null,
                          }).ToListAsync(ct);
        var push = rows.Count(r => r.RemindersEnabled && r.Push);
        var telegram = rows.Count(r => r.RemindersEnabled && !r.Push && r.Telegram);
        return new ReminderChannelsDto(rows.Count, push, telegram, rows.Count - push - telegram);
    }

    /// <summary>L-63: return to card practice 1 and 7 days after the first passed unit, with and without push.</summary>
    private async Task<IReadOnlyList<FirstUnitReturnDto>> FirstUnitReturnAsync(DateOnly today, CancellationToken ct)
    {
        var firsts = (await db.UnitProgress.Where(p => p.PassedAt != null)
                .GroupBy(p => p.UserId)
                .Select(g => new { UserId = g.Key, At = g.Min(p => p.PassedAt)!.Value })
                .ToListAsync(ct))
            .Select(f => (f.UserId, Day: KoreaTime.Day(f.At)))
            .Where(f => f.Day >= today.AddDays(-60) && f.Day <= today.AddDays(-1))
            .ToList();
        var ids = firsts.Select(f => f.UserId).ToList();
        var withPush = (await db.PushSubscriptions.Where(p => ids.Contains(p.UserId)).Select(p => p.UserId).Distinct().ToListAsync(ct)).ToHashSet();
        var reviewSince = time.GetUtcNow().AddDays(-62);
        var reviewed = (await db.ReviewEvents.Where(e => ids.Contains(e.OwnerId) && e.OccurredAt >= reviewSince && e.UndoneAt == null && e.Answer != Domain.Learning.ReviewAnswer.TechnicalError)
                .Select(e => new { e.OwnerId, e.OccurredAt }).ToListAsync(ct))
            .Select(e => (e.OwnerId, KoreaTime.Day(e.OccurredAt))).ToHashSet();

        RetentionDto Return(IReadOnlyCollection<(Guid UserId, DateOnly Day)> group, int after)
        {
            var cohort = group.Where(f => f.Day <= today.AddDays(-after)).ToList();
            var back = cohort.Count(f => reviewed.Contains((f.UserId, f.Day.AddDays(after))));
            return new RetentionDto(cohort.Count, back, cohort.Count == 0 ? null : (int)Math.Round(back * 100.0 / cohort.Count));
        }

        return new[] { true, false }.Select(push =>
        {
            var group = firsts.Where(f => withPush.Contains(f.UserId) == push).ToList();
            return new FirstUnitReturnDto(push, Return(group, 1), Return(group, 7));
        }).ToList();
    }

    /// <summary>Spec §9.2 pilot figure, per open group: members who joined 14+ days ago and were active in days 8–14 after joining.</summary>
    private async Task<IReadOnlyList<GroupPilotDto>> PilotAsync(DateOnly today, CancellationToken ct)
    {
        var groups = await db.Groups.Where(g => g.ClosedAt == null)
            .Select(g => new { g.Id, g.Name, Teacher = db.Users.Where(u => u.Id == g.TeacherUserId).Select(u => u.DisplayName).FirstOrDefault() })
            .ToListAsync(ct);
        var members = (await db.GroupMembers.Where(m => m.LeftAt == null).Select(m => new { m.GroupId, m.UserId, m.JoinedAt }).ToListAsync(ct))
            .ToLookup(m => m.GroupId);
        var ids = members.SelectMany(g => g.Select(m => m.UserId)).Distinct().ToList();
        var days = (await db.ActivityDays.Where(a => ids.Contains(a.UserId)).ToListAsync(ct)).ToLookup(a => a.UserId, a => a.Day);
        return groups.Select(g =>
        {
            var all = members[g.Id].ToList();
            var eligible = all.Where(m => KoreaTime.Day(m.JoinedAt) <= today.AddDays(-14)).ToList();
            var activeSecondWeek = eligible.Count(m =>
            {
                var joined = KoreaTime.Day(m.JoinedAt);
                return days[m.UserId].Any(d => d >= joined.AddDays(7) && d <= joined.AddDays(13));
            });
            return new GroupPilotDto(g.Name, g.Teacher, all.Count, eligible.Count, activeSecondWeek,
                eligible.Count == 0 ? null : (int)Math.Round(activeSecondWeek * 100.0 / eligible.Count));
        }).OrderBy(g => g.Group, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private void Audit(Guid adminId, string action, Guid? target, string detail) =>
        db.AdminAudit.Add(new AdminAuditEntry
        {
            Id = Guid.CreateVersion7(time.GetUtcNow()),
            AdminUserId = adminId,
            Action = action,
            TargetUserId = target,
            Detail = detail.Length > 500 ? detail[..500] : detail,
            At = time.GetUtcNow(),
        });

    private static string RoleLabels(UserRoles roles) => string.Join(", ", RoleNames(roles).Select(r => r switch
    {
        nameof(UserRoles.Learner) => "o‘quvchi",
        nameof(UserRoles.Teacher) => "o‘qituvchi",
        nameof(UserRoles.Editor) => "muharrir",
        nameof(UserRoles.Admin) => "admin",
        _ => r,
    }));

    private static List<string> RoleNames(UserRoles roles) =>
        [.. Enum.GetValues<UserRoles>().Where(r => r != UserRoles.None && roles.HasFlag(r)).Select(r => r.ToString())];
}
