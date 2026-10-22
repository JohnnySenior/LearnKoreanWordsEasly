namespace KoreanAiTeacher.Domain.Exams;

public enum ExamPhase
{
    /// <summary>No exam date set.</summary>
    None,

    /// <summary>More than a week left: new words and grammar are spread over the days before the final week.</summary>
    Study,

    /// <summary>The last 7 days: no new material, only review and timed book tests.</summary>
    FinalWeek,

    ExamDay,

    Past,
}

/// <param name="StudyDays">Days left for new material (today included, the final week excluded).</param>
/// <param name="WordsPerDay">New words to start each day so that all approved words are started before the final week.</param>
/// <param name="GrammarPerWeek">Grammar points to bring to "independent" each week.</param>
/// <param name="MocksPerWeek">Timed book tests suggested each week (TOPIK II reading papers).</param>
public sealed record ExamPlan(
    ExamPhase Phase,
    int DaysLeft,
    int StudyDays,
    int WordsPerDay,
    int GrammarPerWeek,
    int MocksPerWeek);

/// <summary>
/// Spreads what is left over the days before the exam (D-34). Pure arithmetic on dates in the learner's time zone:
/// no promise of a score, only a pace. The final week is kept free of new material.
/// </summary>
public static class ExamPlanner
{
    public const int FinalWeekDays = 7;

    public static ExamPlan Plan(DateOnly today, DateOnly? exam, int newWords, int grammarLeft)
    {
        if (exam is not { } date)
        {
            return new ExamPlan(ExamPhase.None, 0, 0, 0, 0, 0);
        }

        var daysLeft = date.DayNumber - today.DayNumber;
        if (daysLeft < 0)
        {
            return new ExamPlan(ExamPhase.Past, daysLeft, 0, 0, 0, 0);
        }

        if (daysLeft == 0)
        {
            return new ExamPlan(ExamPhase.ExamDay, 0, 0, 0, 0, 0);
        }

        if (daysLeft <= FinalWeekDays)
        {
            return new ExamPlan(ExamPhase.FinalWeek, daysLeft, 0, 0, 0, 2);
        }

        var studyDays = daysLeft - FinalWeekDays;
        return new ExamPlan(
            ExamPhase.Study,
            daysLeft,
            studyDays,
            Ceil(Math.Max(0, newWords), studyDays),
            Ceil(Math.Max(0, grammarLeft) * 7, studyDays),
            daysLeft <= 35 ? 2 : 1);
    }

    private static int Ceil(int total, int days) => total == 0 ? 0 : (total + days - 1) / days;
}
