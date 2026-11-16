namespace Lugat.Contracts;

public sealed record PronunciationDto(string Reading, string? ExplanationUz);

/// <summary>Visible only to the editor/owner; a draft never becomes a learner hint.</summary>
public sealed record PronunciationReviewDto(string? Reading, string? ExplanationUz, bool Reviewed, uint Version);

public sealed record PronunciationVersionRequest(uint Version);

public sealed record ReviewPronunciationRequest(string? Reading, string? ExplanationUz, bool Reviewed, uint Version);
