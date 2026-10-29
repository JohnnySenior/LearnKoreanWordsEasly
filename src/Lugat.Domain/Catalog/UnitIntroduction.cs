namespace Lugat.Domain.Catalog;

public enum IntroductionStatus { Draft, Approved, Ready, Published, Retired }

/// <summary>Immutable input from the human-reviewed catalog. Live word edits never rewrite a published lesson.</summary>
public sealed record IntroductionSource(Guid Id, int Order, string Word, string MeaningUz, string? Romanization,
    string? ExampleKo, string? ExampleUz, string? TrapUz, int ContentVersion);

/// <summary>
/// One board reveal and its own complete recording. No time-sliced narration. Korean named in the narration is
/// written «in Hangul» and spoken by the Korean voice: <see cref="Parts"/> holds one recording per narration part, in
/// order, joined without a gap on the device. Older beats have only <see cref="AudioKey"/> (one Uzbek part).
/// </summary>
public sealed record IntroductionBeat(string Kind, string NarrationUz, string? Korean, string? Uzbek, string? AudioKey = null, List<string>? Parts = null);
public sealed record IntroductionWord(Guid WordId, string Title, List<IntroductionBeat> Beats, bool Reviewed = false, string? KoreanAudioKey = null);

public sealed class UnitIntroduction
{
    public Guid Id { get; set; }
    public Guid UnitId { get; set; }
    public int Revision { get; set; }
    public IntroductionStatus Status { get; set; }
    public List<IntroductionSource> Sources { get; set; } = [];
    public List<IntroductionWord> Words { get; set; } = [];
    public string SourceHash { get; set; } = "";
    public string? NarratorProfile { get; set; }
    public string? KoreanProfile { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? ApprovedBy { get; set; }
    public Guid? PublishedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public uint Version { get; set; }
}
