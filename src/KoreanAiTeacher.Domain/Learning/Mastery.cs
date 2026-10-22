namespace KoreanAiTeacher.Domain.Learning;

/// <summary>
/// "O‘rganildi" rule from spec SRS06 (technical baseline, may change):
/// in each direction at least 3 independent successes on distinct days, interval at least 7 days,
/// and no lapse in the last 7 days.
/// </summary>
public static class Mastery
{
    public const int RequiredSuccessDays = 3;
    public static readonly TimeSpan MinInterval = TimeSpan.FromDays(7);
    public static readonly TimeSpan LapseQuietPeriod = TimeSpan.FromDays(7);

    public static bool IsMastered(LearningState state, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Phase == LearningPhase.Review
            && state.IndependentSuccessDays >= RequiredSuccessDays
            && state.Interval >= MinInterval
            && (state.LastLapseAt is null || now - state.LastLapseAt.Value >= LapseQuietPeriod);
    }

    /// <summary>A unit counts as learned only when both directions are mastered.</summary>
    public static bool IsMastered(LearningState koreanToUzbek, LearningState uzbekToKorean, DateTimeOffset now) =>
        IsMastered(koreanToUzbek, now) && IsMastered(uzbekToKorean, now);
}
