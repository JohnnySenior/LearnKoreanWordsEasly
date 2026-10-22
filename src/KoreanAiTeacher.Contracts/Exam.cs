using KoreanAiTeacher.Domain.Exams;

namespace KoreanAiTeacher.Contracts;

/// <summary>Exam countdown and pace (D-34). Days are counted in the learner's time zone.</summary>
/// <param name="NewWords">Approved words not started yet.</param>
/// <param name="GrammarLeft">Approved grammar points not yet used independently.</param>
/// <param name="MocksLast7Days">Book tests finished in exam mode during the last 7 days.</param>
/// <param name="LastMockScore">Last exam-mode book test on a 100-point scale.</param>
public sealed record ExamDto(
    DateOnly? ExamDate,
    int? TargetLevel,
    ExamPhase Phase,
    int DaysLeft,
    int StudyDays,
    int NewWords,
    int WordsPerDay,
    int WordsStartedToday,
    int GrammarLeft,
    int GrammarPerWeek,
    int MocksPerWeek,
    int MocksLast7Days,
    int BookTestsReady,
    int? LastMockScore,
    DateTimeOffset? LastMockAt);

/// <param name="ExamDate">Null clears the date.</param>
/// <param name="TargetLevel">3–6 or null.</param>
public sealed record UpdateExamRequest(DateOnly? ExamDate, int? TargetLevel);
