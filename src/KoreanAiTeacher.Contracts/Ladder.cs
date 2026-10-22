using KoreanAiTeacher.Domain.Ladder;

namespace KoreanAiTeacher.Contracts;

/// <param name="Id">Chosen by the client, so a double tap never makes two ladders (API01).</param>
/// <param name="WeakPoints">Build it from the learner's weak points of the last 30 days (D-54).</param>
public sealed record CreateLadderRequest(Guid Id, bool WeakPoints = false);

/// <summary>The task to do now (D-47).</summary>
/// <param name="Number">1-based position inside the step, of <paramref name="OfStep"/>.</param>
/// <param name="Uzbek">Fill: the sentence's meaning; Translate: what to translate; Order: the meaning as a guide.</param>
/// <param name="Options">Fill: the right particle or ending and its look-alikes, shuffled.</param>
/// <param name="FocusKind">Fill: "qo‘shimcha" or "fe’l shakli".</param>
/// <param name="Chunks">Order: the sentence's word groups, shuffled.</param>
public sealed record LadderTaskDto(
    int Index,
    LadderStep Step,
    int Number,
    int OfStep,
    string Uzbek,
    string? SentenceWithGap,
    IReadOnlyList<string> Options,
    string? FocusKind,
    IReadOnlyList<string> Chunks);

public sealed record LadderAnswerRequest(int Index, LadderStep Step, string Answer);

/// <param name="Correct">The right sentence or form.</param>
/// <param name="Corrected">Translate: the learner's own sentence corrected.</param>
/// <param name="Trap">A word-for-word mistake written to "Tuzoqlar daftari".</param>
public sealed record LadderFeedbackDto(
    bool IsCorrect,
    string Correct,
    string? Corrected,
    string? ExplanationUz,
    IReadOnlyList<string> ErrorTypes,
    TrapNoteDto? Trap);

public sealed record LadderStepScoreDto(LadderStep Step, int Correct, int Answered, int Total);

public sealed record LadderResultDto(int Index, LadderStep Step, string Uzbek, string Given, bool IsCorrect, string Correct, string? Corrected, string? ExplanationUz, IReadOnlyList<string> ErrorTypes);

public sealed record LadderDto(
    Guid Id,
    string Title,
    DateTimeOffset CreatedAt,
    bool Finished,
    int Done,
    int Total,
    LadderTaskDto? Current,
    IReadOnlyList<LadderStepScoreDto> Scores,
    IReadOnlyList<LadderResultDto> Results,
    string? Provider);

public sealed record LadderAnswerResultDto(LadderFeedbackDto Feedback, LadderDto Ladder);

public sealed record LadderSummaryDto(Guid Id, string Title, DateTimeOffset CreatedAt, bool Finished, int Done, int Total, int Correct);

public sealed record TrapNoteDto(Guid Id, string Uzbek, string Wrong, string Right, string ExplanationUz, DateTimeOffset CreatedAt);
