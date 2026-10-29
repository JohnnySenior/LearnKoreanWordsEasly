using Lugat.Domain.Catalog;
namespace Lugat.Contracts;

public sealed record IntroductionBeatDto(string Kind, string NarrationUz, string? Korean, string? Uzbek, string? AudioUrl);
public sealed record IntroductionWordDto(Guid WordId, string Word, string MeaningUz, string? Romanization, string Title,
    bool HasTrap, bool Reviewed, string? KoreanAudioUrl, IReadOnlyList<IntroductionBeatDto> Beats);
public sealed record IntroductionDto(Guid Id, Guid UnitId, int Revision, IntroductionStatus Status, uint Version,
    string UnitLabel, bool SourceChanged, int TotalWords, int RemainingWords, int RemainingAudio,
    IReadOnlyList<IntroductionWordDto> Words, Guid? ApprovedBy, DateTimeOffset? ApprovedAt, DateTimeOffset? PublishedAt);
public sealed record IntroductionAvailabilityDto(IntroductionDto? Lesson);
public sealed record IntroductionListDto(IReadOnlyList<IntroductionDto> Versions, bool NarratorConfigured);
public sealed record CreateIntroductionRequest(Guid Id);
public sealed record IntroductionVersionRequest(uint Version);
public sealed record PublishIntroductionRequest(uint Version, bool AudioReviewed);
public sealed record IntroductionBeatInput(string Kind, string NarrationUz, string? Korean, string? Uzbek);
public sealed record UpdateIntroductionWordRequest(uint Version, string Title, IReadOnlyList<IntroductionBeatInput> Beats, bool Reviewed);
