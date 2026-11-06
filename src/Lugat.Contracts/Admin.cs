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

public sealed record TeacherCommissionDto(Guid TeacherUserId, string? Teacher, DateOnly Month, int Payments, int Amount, int Commission, bool Paid, DateTimeOffset? PaidAt, Currency Currency = Currency.Krw);

public sealed record MarkCommissionPaidRequest(Guid TeacherUserId, DateOnly Month, Currency Currency = Currency.Krw);

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

public sealed record RevenueMonthDto(DateOnly Month, int Payments, int Amount, int Commission, Currency Currency = Currency.Krw);

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
    IReadOnlyList<GroupPilotDto> Groups,
    ReminderChannelsDto? Channels = null,
    IReadOnlyList<FirstUnitReturnDto>? FirstUnitReturn = null,
    TrialMetricsDto? Trials = null);

/// <summary>
/// L-64: trials started in the last 7 and 30 days; of the trials that ended in the last 60 days, how many learners
/// paid between the trial's start and 14 days after its end; and Premium page views per locked feature in 30 days.
/// </summary>
public sealed record TrialMetricsDto(int Started7, int Started30, int Ended60, int PaidAfter, IReadOnlyList<FeatureViewsDto> Views30);

public sealed record FeatureViewsDto(string Feature, int Views);

/// <summary>
/// L-63: of the learners active in the last 30 days, how a reminder can reach them now. Reminders off counts as none;
/// a learner with a device and Telegram counts as push, the channel that is used.
/// </summary>
public sealed record ReminderChannelsDto(int Active, int Push, int Telegram, int None);

/// <summary>
/// L-63: learners who passed their first unit 1+ (7+) days ago, within the last 60 days, and practised cards exactly
/// 1 (7) days after that day — split by whether they have a push subscription now.
/// </summary>
public sealed record FirstUnitReturnDto(bool WithPush, RetentionDto Day1, RetentionDto Day7);

/// <summary>One ad channel (L-40): visits with its link, sign-ups that came with it, their D7 return and how many paid.</summary>
public sealed record SourceRowDto(string Source, int Visits, int SignUps, RetentionDto D7, int Paying);

public sealed record SourceRequest(string? Source);
