namespace Lugat.Contracts;

/// <param name="Rules">L-83: the standard rules that make the reading, found by the server from the spelling.</param>
public sealed record PronunciationDto(string Reading, string? ExplanationUz, IReadOnlyList<PronunciationRuleDto>? Rules = null);

/// <summary>L-83: one standard pronunciation rule: Uzbek name, Korean name, and what changes where ("국물: ㄱ ㅁ oldidan ㅇ bo‘ladi").</summary>
public sealed record PronunciationRuleDto(string NameUz, string NameKo, string Detail);

/// <summary>Visible only to the editor/owner; a draft never becomes a learner hint.</summary>
/// <param name="Rules">L-83: the rules found for the reading as it stands.</param>
/// <param name="RulesComplete">False when the reading differs from the spelling in a way no standard rule explains: check it.</param>
public sealed record PronunciationReviewDto(string? Reading, string? ExplanationUz, bool Reviewed, uint Version, DateTimeOffset? AskedAt = null,
    IReadOnlyList<PronunciationRuleDto>? Rules = null, bool RulesComplete = true);

public sealed record PronunciationVersionRequest(uint Version);

public sealed record ReviewPronunciationRequest(string? Reading, string? ExplanationUz, bool Reviewed, uint Version);


public sealed record PronunciationWordReviewInput(Guid Id, string? Reading, string? ExplanationUz, uint Version);

public sealed record ReviewPronunciationsRequest(IReadOnlyList<PronunciationWordReviewInput> Words);


/// <summary>One bounded preparation step, chosen by the server. The client continues until Done or the user stops.</summary>
public sealed record UnitPreparationDto(UnitAdminDto Unit, string Step, int Remaining, bool Done);
