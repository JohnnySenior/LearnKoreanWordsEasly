namespace Lugat.Contracts;

public sealed record PronunciationDto(string Reading, string? ExplanationUz);

/// <summary>Visible only to the editor/owner; a draft never becomes a learner hint.</summary>
public sealed record PronunciationReviewDto(string? Reading, string? ExplanationUz, bool Reviewed, uint Version, DateTimeOffset? AskedAt = null);

public sealed record PronunciationVersionRequest(uint Version);

public sealed record ReviewPronunciationRequest(string? Reading, string? ExplanationUz, bool Reviewed, uint Version);


public sealed record PronunciationWordReviewInput(Guid Id, string? Reading, string? ExplanationUz, uint Version);

public sealed record ReviewPronunciationsRequest(IReadOnlyList<PronunciationWordReviewInput> Words);


/// <summary>One bounded preparation step, chosen by the server. The client continues until Done or the user stops.</summary>
public sealed record UnitPreparationDto(UnitAdminDto Unit, string Step, int Remaining, bool Done);
