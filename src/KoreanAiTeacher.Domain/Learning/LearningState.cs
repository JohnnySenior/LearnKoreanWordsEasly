namespace KoreanAiTeacher.Domain.Learning;

/// <summary>Which side of a card is asked. Each direction has its own schedule (spec SRS01).</summary>
public enum ReviewDirection
{
    KoreanToUzbek,
    UzbekToKorean,
}

/// <summary>What the learner (or the system) reported after answering (spec SRS02, SRS04).</summary>
public enum ReviewAnswer
{
    /// <summary>"Eslay olmadim".</summary>
    Forgot,

    /// <summary>"Qiynalib esladim".</summary>
    Hard,

    /// <summary>"Oson esladim".</summary>
    Easy,

    /// <summary>STT/API failure. Never counted as a knowledge result and never penalises the schedule.</summary>
    TechnicalError,
}

public enum LearningPhase
{
    New,
    Relearning,
    Review,
}

/// <summary>
/// Schedule of one learning unit (word sense or grammar usage) in one direction for one user.
/// Immutable: schedulers return a new instance.
/// </summary>
public sealed record LearningState
{
    public LearningPhase Phase { get; init; } = LearningPhase.New;

    /// <summary>Index into the scheduler's interval ladder, or -1 when the card has no ladder position yet.</summary>
    public int Stage { get; init; } = -1;

    public TimeSpan Interval { get; init; } = TimeSpan.Zero;

    /// <summary>Null for a new card: it is available immediately.</summary>
    public DateTimeOffset? DueAt { get; init; }

    /// <summary>Times a card in <see cref="LearningPhase.Review"/> was forgotten.</summary>
    public int Lapses { get; init; }

    public DateTimeOffset? LastLapseAt { get; init; }

    public DateTimeOffset? LastReviewedAt { get; init; }

    /// <summary>Distinct local calendar days with an unassisted, on-time success (spec SRS06).</summary>
    public int IndependentSuccessDays { get; init; }

    public DateOnly? LastIndependentSuccessDay { get; init; }

    public string AlgorithmVersion { get; init; } = LadderSchedulerV1.Version;

    public static LearningState New() => new();

    public bool IsDue(DateTimeOffset now) => DueAt is null || now >= DueAt.Value;
}

/// <param name="State">State after the review.</param>
/// <param name="WasDue">False when the card was drilled before its due time.</param>
/// <param name="CountsAsIndependentSuccess">Unassisted success on a due card.</param>
public sealed record ScheduleResult(LearningState State, bool WasDue, bool CountsAsIndependentSuccess);

/// <summary>Replaceable scheduling algorithm (spec SRS04).</summary>
public interface ISpacedRepetitionScheduler
{
    string AlgorithmVersion { get; }

    /// <param name="localDay">The learner's calendar day in their own time zone, used for SRS06 day counting.</param>
    ScheduleResult Apply(LearningState state, ReviewAnswer answer, bool assisted, DateTimeOffset now, DateOnly localDay);
}
