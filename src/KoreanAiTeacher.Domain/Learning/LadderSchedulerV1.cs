namespace KoreanAiTeacher.Domain.Learning;

/// <summary>
/// Simple versioned interval ladder from spec SRS04: 5h, 1d, 3d, 7d, 14d, 30d, 60d.
/// This is intentionally not FSRS and makes no optimality claim.
/// </summary>
/// <remarks>
/// Rules (spec §6 table):
/// <list type="bullet">
/// <item>Forgot: back in ~15 minutes, relearning; the next success restarts at 5h. Counts as a lapse only for review cards.</item>
/// <item>Hard: new/relearning card goes to 5h; a review card gets half its interval (min 5h) and keeps its stage.</item>
/// <item>Easy: new card goes to 1 day, relearning card to 5h, review card one stage up.</item>
/// <item>Assisted (hint used): never an independent success and never promoted beyond what Hard would give.</item>
/// <item>Drilled before due: Hard/Easy change nothing, so an extra session cannot inflate the interval. Forgot still applies.</item>
/// <item>Technical error: no change at all.</item>
/// </list>
/// </remarks>
public sealed class LadderSchedulerV1 : ISpacedRepetitionScheduler
{
    public const string Version = "ladder-v1";

    public static readonly IReadOnlyList<TimeSpan> Ladder =
    [
        TimeSpan.FromHours(5),
        TimeSpan.FromDays(1),
        TimeSpan.FromDays(3),
        TimeSpan.FromDays(7),
        TimeSpan.FromDays(14),
        TimeSpan.FromDays(30),
        TimeSpan.FromDays(60),
    ];

    public static readonly TimeSpan RelearnDelay = TimeSpan.FromMinutes(15);

    public string AlgorithmVersion => Version;

    public ScheduleResult Apply(LearningState state, ReviewAnswer answer, bool assisted, DateTimeOffset now, DateOnly localDay)
    {
        ArgumentNullException.ThrowIfNull(state);
        var wasDue = state.IsDue(now);

        if (answer == ReviewAnswer.TechnicalError)
        {
            return new ScheduleResult(state, wasDue, CountsAsIndependentSuccess: false);
        }

        if (answer == ReviewAnswer.Forgot)
        {
            var isLapse = state.Phase == LearningPhase.Review;
            var forgotten = state with
            {
                Phase = LearningPhase.Relearning,
                Stage = -1,
                Interval = RelearnDelay,
                DueAt = now + RelearnDelay,
                Lapses = isLapse ? state.Lapses + 1 : state.Lapses,
                LastLapseAt = isLapse ? now : state.LastLapseAt,
                LastReviewedAt = now,
                AlgorithmVersion = Version,
            };
            return new ScheduleResult(forgotten, wasDue, CountsAsIndependentSuccess: false);
        }

        if (!wasDue)
        {
            return new ScheduleResult(state with { LastReviewedAt = now }, wasDue, CountsAsIndependentSuccess: false);
        }

        var effective = assisted ? ReviewAnswer.Hard : answer;
        var (stage, interval) = NextPosition(state, effective);
        var independent = !assisted;

        var next = state with
        {
            Phase = LearningPhase.Review,
            Stage = stage,
            Interval = interval,
            DueAt = now + interval,
            LastReviewedAt = now,
            AlgorithmVersion = Version,
        };

        if (independent && next.LastIndependentSuccessDay != localDay)
        {
            next = next with
            {
                IndependentSuccessDays = next.IndependentSuccessDays + 1,
                LastIndependentSuccessDay = localDay,
            };
        }

        return new ScheduleResult(next, wasDue, independent);
    }

    private static (int Stage, TimeSpan Interval) NextPosition(LearningState state, ReviewAnswer answer)
    {
        var first = Ladder[0];

        if (state.Phase != LearningPhase.Review)
        {
            // New card: Easy skips straight to 1 day. Relearning restarts at 5h either way.
            return answer == ReviewAnswer.Easy && state.Phase == LearningPhase.New
                ? (1, Ladder[1])
                : (0, first);
        }

        if (answer == ReviewAnswer.Hard)
        {
            var halved = state.Interval / 2;
            return (state.Stage, halved < first ? first : halved);
        }

        var stage = Math.Min(state.Stage + 1, Ladder.Count - 1);
        return (stage, Ladder[stage]);
    }
}
