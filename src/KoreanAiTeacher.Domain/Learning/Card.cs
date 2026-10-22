namespace KoreanAiTeacher.Domain.Learning;

/// <summary>
/// Persisted schedule of one word sense in one direction for its owner (spec §12 LearningState).
/// The scheduling rules live in <see cref="ISpacedRepetitionScheduler"/>; this class only stores the result.
/// </summary>
public sealed class Card
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public Guid WordSenseId { get; set; }

    public ReviewDirection Direction { get; set; }

    public LearningPhase Phase { get; set; }

    public int Stage { get; set; } = -1;

    public TimeSpan Interval { get; set; }

    public DateTimeOffset? DueAt { get; set; }

    public int Lapses { get; set; }

    public DateTimeOffset? LastLapseAt { get; set; }

    public DateTimeOffset? LastReviewedAt { get; set; }

    public int IndependentSuccessDays { get; set; }

    public DateOnly? LastIndependentSuccessDay { get; set; }

    public string AlgorithmVersion { get; set; } = LadderSchedulerV1.Version;

    /// <summary>Optimistic concurrency token: two devices answering at once cannot both win (spec API01).</summary>
    public uint Version { get; set; }

    public LearningState ToState() => new()
    {
        Phase = Phase,
        Stage = Stage,
        Interval = Interval,
        DueAt = DueAt,
        Lapses = Lapses,
        LastLapseAt = LastLapseAt,
        LastReviewedAt = LastReviewedAt,
        IndependentSuccessDays = IndependentSuccessDays,
        LastIndependentSuccessDay = LastIndependentSuccessDay,
        AlgorithmVersion = AlgorithmVersion,
    };

    public void Apply(LearningState state)
    {
        Phase = state.Phase;
        Stage = state.Stage;
        Interval = state.Interval;
        DueAt = state.DueAt;
        Lapses = state.Lapses;
        LastLapseAt = state.LastLapseAt;
        LastReviewedAt = state.LastReviewedAt;
        IndependentSuccessDays = state.IndependentSuccessDays;
        LastIndependentSuccessDay = state.LastIndependentSuccessDay;
        AlgorithmVersion = state.AlgorithmVersion;
    }
}

/// <summary>
/// One answer, recorded with everything needed to audit or undo it (spec SRS05).
/// <see cref="Id"/> is chosen by the client, so a retried request is recognised and applied only once.
/// </summary>
public sealed class ReviewEvent
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public Guid? SessionId { get; set; }

    public int? SessionPosition { get; set; }

    public Guid CardId { get; set; }

    public Guid WordSenseId { get; set; }

    public ReviewDirection Direction { get; set; }

    public ReviewAnswer Answer { get; set; }

    public bool Assisted { get; set; }

    public bool WasDue { get; set; }

    public bool CountedAsIndependentSuccess { get; set; }

    /// <summary>Time from showing the prompt to answering, as measured by the client. Used to size sessions (spec PLAN06).</summary>
    public int? ResponseMs { get; set; }

    /// <summary>Server time, UTC (spec SRS05).</summary>
    public DateTimeOffset OccurredAt { get; set; }

    public string AlgorithmVersion { get; set; } = "";

    public LearningState PreviousState { get; set; } = new();

    public LearningState NextState { get; set; } = new();

    /// <summary>Where a forgotten card was put back into the session queue, so an undo can take it out again.</summary>
    public int? ReinsertedAt { get; set; }

    public DateTimeOffset? UndoneAt { get; set; }
}
