using Lugat.Domain.Premium;

namespace Lugat.Contracts;

// The admin panel (spec §10). Only admins; the content part (catalog) is in Catalog.cs and open to editors too.

public sealed record SubscriptionDto(Guid Id, SubscriptionPlan Plan, SubscriptionSource Source, DateTimeOffset StartsAt, DateTimeOffset EndsAt);

public sealed record AuditEntryDto(DateTimeOffset At, string Action, string Detail, string? Admin);

/// <param name="PromoCode">The teacher's code the learner came with; the payment form fills it in.</param>
/// <param name="Groups">Groups the user is a learner in ("guruh — o‘qituvchi").</param>
public sealed record AdminUserDetailDto(
    Guid Id,
    string? DisplayName,
    string? PaymentCode,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Logins,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt,
    bool IsPremium,
    DateTimeOffset? PremiumEndsAt,
    string? PromoCode,
    IReadOnlyList<SubscriptionDto> Subscriptions,
    IReadOnlyList<StudentUnitDto> Units,
    IReadOnlyList<string> Groups,
    IReadOnlyList<AuditEntryDto> Audit);

/// <param name="Roles">"Teacher", "Editor", "Admin"; every user is a learner.</param>
public sealed record SetRolesRequest(IReadOnlyList<string> Roles);

/// <param name="Days">Positive: Premium days given after the current period. Negative: days taken off its end.</param>
/// <param name="Reason">Required (spec §10: "sabab bilan").</param>
public sealed record AdjustPremiumRequest(int Days, string Reason);

public sealed record SetPromoActiveRequest(bool IsActive);

public sealed record TeacherCommissionDto(Guid TeacherUserId, string? Teacher, DateOnly Month, int Payments, int AmountKrw, int CommissionKrw, bool Paid, DateTimeOffset? PaidAt);

public sealed record MarkCommissionPaidRequest(Guid TeacherUserId, DateOnly Month);

// Spending (spec §10 "Sarf"): AI and voice calls from ai_usage.

public sealed record UsageDayDto(DateOnly Day, int Calls, int Failed, decimal Usd);

/// <param name="Amount">Tokens, characters or audio milliseconds, as <paramref name="Unit"/> says.</param>
public sealed record UsageFeatureDto(string Feature, string Unit, int Calls, long Amount, decimal Usd, int UnknownPrice);

public sealed record UsageReportDto(int Days, IReadOnlyList<UsageDayDto> ByDay, IReadOnlyList<UsageFeatureDto> ByFeature, AiBudgetDto Month);

// Key figures (spec §10): a plain table, no charts. Days are Korea days.

public sealed record ActiveDayDto(DateOnly Day, int Users);

/// <param name="Cohort">Users old enough to be counted.</param>
public sealed record RetentionDto(int Cohort, int Returned, int? Percent);

public sealed record UnitPassRateDto(string Unit, int Attempts, int Passed, int? Percent);

public sealed record RevenueMonthDto(DateOnly Month, int Payments, int AmountKrw, int CommissionKrw);

/// <summary>The pilot figure (spec §9.2): of the learners who joined at least 14 days ago, how many were active in their second week.</summary>
public sealed record GroupPilotDto(string Group, string? Teacher, int Members, int Eligible, int ActiveSecondWeek, int? Percent);

public sealed record MetricsDto(
    int Users,
    int NewLast7Days,
    int NewLast30Days,
    IReadOnlyList<ActiveDayDto> DailyActive,
    RetentionDto D1,
    RetentionDto D7,
    int PremiumNow,
    IReadOnlyList<RevenueMonthDto> Revenue,
    IReadOnlyList<UnitPassRateDto> Units,
    IReadOnlyList<GroupPilotDto> Groups);

/// <summary>One ad channel (L-40): visits with its link, sign-ups that came with it, their D7 return and how many paid.</summary>
public sealed record SourceRowDto(string Source, int Visits, int SignUps, RetentionDto D7, int Paying);

public sealed record SourceRequest(string? Source);
