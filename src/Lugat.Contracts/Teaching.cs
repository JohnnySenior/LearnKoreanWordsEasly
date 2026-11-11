using Lugat.Domain.Catalog;
using Lugat.Domain.Premium;
using Lugat.Domain.Teaching;

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
/// <param name="Reviewed7">Cards the learner answered in the last 7 days (each card once, L-61).</param>
/// <param name="DueNow">Cards waiting for their review right now.</param>
public sealed record GroupStudentDto(
    Guid UserId,
    string? Name,
    string? CurrentUnit,
    int PassedUnits,
    int? LastTestPercent,
    int ActiveDays7,
    DateTimeOffset LastSeenAt,
    DateTimeOffset JoinedAt,
    int Reviewed7 = 0,
    int DueNow = 0);

/// <param name="Learners">How many learners of the group missed it.</param>
/// <param name="Sources">Where the mistakes were made: "kartochka", "bo‘lim testi", "yozma"… (L-61).</param>
public sealed record MissedWordDto(string Korean, string Uzbek, string? Unit, int Mistakes, int Learners, IReadOnlyList<string>? Sources = null);

public sealed record GroupDetailDto(Guid Id, string Name, string InviteCode, IReadOnlyList<GroupStudentDto> Students, IReadOnlyList<MissedWordDto> HardestWords);

public sealed record StudentUnitDto(string Unit, UnitProgressStatus Status, int? BestScorePercent, int Attempts, UnitPassedBy? PassedBy);

public sealed record StudentDetailDto(Guid UserId, string? Name, string? CurrentUnit, IReadOnlyList<StudentUnitDto> Units, IReadOnlyList<MissedWordDto> TopMistakes, StudentWeekDto? Week = null,
    IReadOnlyList<StudentDeckDto>? Decks = null);

/// <summary>The learner's last 7 days (L-61): counts only, never the answers themselves.</summary>
/// <param name="Practice">Each exercise the learner did, in the Mashq page's order; ones not done are left out.</param>
public sealed record StudentWeekDto(int Reviewed, int DueNow, IReadOnlyList<PracticeCountDto> Practice);

/// <param name="Name">The exercise as the Mashq page names it.</param>
/// <param name="Count">Words written for "Yozma mashq", otherwise how many times it was done.</param>
public sealed record PracticeCountDto(string Name, int Count, bool Words = false);

public sealed record TeacherPromoDto(string Code, int DiscountPercent, int CommissionPercent, bool IsActive);

/// <param name="Month">The first day of the month, Korea time.</param>
public sealed record CommissionMonthDto(DateOnly Month, int Payments, int Amount, int Commission, bool Paid, DateTimeOffset? PaidAt, Currency Currency = Currency.Krw);

/// <summary>Counts and sums only: who paid is never shown to the teacher (spec §9.3).</summary>
/// <param name="Totals">Paid and commission, one row per currency (won and so‘m are never added together, L-48).</param>
public sealed record CommissionReportDto(IReadOnlyList<TeacherPromoDto> Codes, IReadOnlyList<CommissionMonthDto> Months, int Payments, IReadOnlyList<MoneyTotalDto> Totals);

public sealed record MoneyTotalDto(Currency Currency, int Amount, int Commission);

// A teacher's own vocabulary lists for a group (L-67).

public sealed record GroupDeckSummaryDto(Guid Id, string Name, GroupDeckStatus Status, int Words, DateTimeOffset CreatedAt, DateTimeOffset? PublishedAt);

/// <param name="Id">Chosen by the page once, so a double tap makes one deck (API01).</param>
public sealed record CreateGroupDeckRequest(Guid Id, string Name);

public sealed record RenameGroupDeckRequest(string Name);

/// <param name="Shared">The learners share the course's item for this word (the teacher's translation is one of its meanings).</param>
/// <param name="CatalogUnit">The unit the catalog word is in, when the catalog has the word.</param>
public sealed record GroupDeckWordDto(
    Guid Id,
    int Order,
    string Korean,
    string Uzbek,
    GroupWordSource Source,
    bool Shared,
    string? CatalogUnit,
    string? Romanization,
    string? ExampleKo,
    string? ExampleUz,
    string? TrapUz,
    TrapSceneDto? TrapScene);

/// <param name="CanPublish">The teacher has the paid plan that AI filling and publishing need.</param>
/// <param name="AiPending">Words the catalog does not have and the AI has not filled yet.</param>
public sealed record GroupDeckDto(
    Guid Id,
    Guid GroupId,
    string GroupName,
    string Name,
    GroupDeckStatus Status,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<GroupDeckWordDto> Words,
    int Members,
    bool CanPublish,
    int AiPending);

/// <param name="Problems">Why rows were skipped, in Uzbek, at most 20.</param>
public sealed record GroupDeckImportDto(GroupDeckDto Deck, int Added, int Skipped, IReadOnlyList<string> Problems);

public sealed record AddGroupWordRequest(string Korean, string Uzbek);

public sealed record UpdateGroupWordRequest(string Korean, string Uzbek, string? Romanization, string? ExampleKo, string? ExampleUz, string? TrapUz);

/// <param name="Left">Words still without content after this call (another call fills the next ten).</param>
public sealed record GroupDeckFillDto(GroupDeckDto Deck, int Filled, int Left);

// The teacher's report on one deck (L-68): what each learner did with exactly these words. Counts and scores only,
// never the answers themselves (spec §9.2).

/// <param name="Learned">Words with both cards mastered; <paramref name="Learning"/> started; <paramref name="New"/> not yet seen.</param>
/// <param name="Reviewed7">Cards of the deck answered in the last 7 days, each card once.</param>
/// <param name="LastTestPercent">The last finished test of the deck, in percent of the counted questions.</param>
public sealed record DeckStudentDto(
    Guid UserId,
    string? Name,
    int Words,
    int Learned,
    int Learning,
    int New,
    int Reviewed7,
    int Tests,
    int? LastTestPercent,
    DateTimeOffset? LastTestAt,
    int? BestTestPercent);

/// <param name="Learned">Learners who learned the word.</param>
/// <param name="Mistakes">Mistakes on it in the last 60 days, all learners together; <paramref name="Learners"/> who made them.</param>
public sealed record DeckWordStatDto(string Korean, string Uzbek, int Learned, int Mistakes, int Learners);

public sealed record GroupDeckReportDto(Guid DeckId, string Name, int Words, IReadOnlyList<DeckStudentDto> Students, IReadOnlyList<DeckWordStatDto> WordStats);

/// <summary>One teacher's deck on a learner's page (L-68).</summary>
public sealed record StudentDeckDto(Guid DeckId, string Name, int Words, int Learned, int? LastTestPercent, DateTimeOffset? LastTestAt);
