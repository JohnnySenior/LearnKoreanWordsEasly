using Lugat.Domain.Catalog;

namespace Lugat.Contracts;

// Groups, from the learner's side (spec §9.1).

/// <summary>What the learner agrees to before joining a group.</summary>
public sealed record GroupInviteDto(string GroupName, string? TeacherName, bool AlreadyMember);

public sealed record MyGroupDto(Guid Id, string Name, string? TeacherName, DateTimeOffset JoinedAt);

/// <summary>A teacher's promo code the learner came with (<c>/?ref=CODE</c>).</summary>
public sealed record ReferralRequest(string Code);

// The teacher panel (spec §9.2, §9.3). A teacher sees only these fields: no answers, payments or logins.

public sealed record TeacherGroupDto(Guid Id, string Name, string InviteCode, int Members, DateTimeOffset CreatedAt);

/// <param name="Id">Chosen by the page once, so a double tap makes one group (API01).</param>
public sealed record CreateGroupRequest(Guid Id, string Name);

/// <param name="ActiveDays7">Days in the last 7 (Korea time) the learner opened the app.</param>
/// <param name="LastTestPercent">Score of the learner's latest unit test.</param>
public sealed record GroupStudentDto(
    Guid UserId,
    string? Name,
    string? CurrentUnit,
    int PassedUnits,
    int? LastTestPercent,
    int ActiveDays7,
    DateTimeOffset LastSeenAt,
    DateTimeOffset JoinedAt);

/// <param name="Learners">How many learners of the group missed it.</param>
public sealed record MissedWordDto(string Korean, string Uzbek, string? Unit, int Mistakes, int Learners);

public sealed record GroupDetailDto(Guid Id, string Name, string InviteCode, IReadOnlyList<GroupStudentDto> Students, IReadOnlyList<MissedWordDto> HardestWords);

public sealed record StudentUnitDto(string Unit, UnitProgressStatus Status, int? BestScorePercent, int Attempts, UnitPassedBy? PassedBy);

public sealed record StudentDetailDto(Guid UserId, string? Name, string? CurrentUnit, IReadOnlyList<StudentUnitDto> Units, IReadOnlyList<MissedWordDto> TopMistakes);

public sealed record TeacherPromoDto(string Code, int DiscountPercent, int CommissionPercent, bool IsActive);

/// <param name="Month">The first day of the month, Korea time.</param>
public sealed record CommissionMonthDto(DateOnly Month, int Payments, int AmountKrw, int CommissionKrw, bool Paid, DateTimeOffset? PaidAt);

/// <summary>Counts and sums only: who paid is never shown to the teacher (spec §9.3).</summary>
public sealed record CommissionReportDto(IReadOnlyList<TeacherPromoDto> Codes, IReadOnlyList<CommissionMonthDto> Months, int Payments, int AmountKrw, int CommissionKrw);
