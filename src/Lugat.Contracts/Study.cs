using Lugat.Domain.Learning;
using Lugat.Domain.Study;
using Lugat.Domain.Vocabulary;

namespace Lugat.Contracts;

/// <param name="NewAvailable">New cards of the current unit(s), the only place new words come from (spec §4.6).</param>
/// <param name="CurrentUnit">The open unit to continue (the newest one), or null when there is none.</param>
public sealed record TodayDto(
    int DueNow,
    int NewAvailable,
    SessionDto? ActiveSession,
    int SecondsPerCard,
    UnitSummaryDto? CurrentUnit = null);

/// <param name="Minutes">10, 20, 30 or null for no limit (spec PLAN06).</param>
/// <param name="Restart">Abandon an unfinished session and plan a new one.</param>
/// <param name="UnitId">Practise only this unit, all its words, also those not yet due (spec §4.3 step 2, D-36).</param>
/// <param name="LearningItemIds">Practise only these words, all their cards (the mistakes notebook, D-41).</param>
public sealed record StartSessionRequest(int? Minutes, bool Restart, Guid? UnitId = null, IReadOnlyList<Guid>? LearningItemIds = null);

public sealed record StartSessionResponse(SessionDto? Session, string? Message);

/// <summary>Approximate next interval for each rating, shown on the buttons (spec UX03).</summary>
public sealed record RatingPreview(ReviewAnswer Answer, int IntervalSeconds, bool ChangesSchedule);

public sealed record CardPromptDto(
    int Position,
    Guid CardId,
    Guid LearningItemId,
    ReviewDirection Direction,
    SessionItemKind Kind,
    string Prompt,
    string? PromptNote,
    string Answer,
    string? AnswerNote,
    string? Unit,
    bool IsDue,
    IReadOnlyList<RatingPreview> Previews,
    string? UsageTrap = null,
    string? MeaningHint = null);

public sealed record SessionSummaryDto(int Answered, int Forgot, int Hard, int Easy, int NewSeen);

public sealed record SessionDto(
    Guid Id,
    StudySessionStatus Status,
    int? Minutes,
    int Position,
    int Total,
    CardPromptDto? Current,
    SessionSummaryDto Summary,
    Guid? UndoableEventId);

/// <param name="EventId">Chosen by the client once per answer and reused on retries (spec API01, SRS05).</param>
/// <param name="Position">The position the answer belongs to, so a stale screen cannot answer the wrong card.</param>
public sealed record AnswerRequest(Guid EventId, int Position, ReviewAnswer Answer, bool Assisted, int? ResponseMs);

public sealed record AnswerResultDto(Guid EventId, DateTimeOffset? NextDueAt, int IntervalSeconds, SessionDto Session);

/// <summary>One word to type (spec PR02). The answer is not sent until the learner has answered.</summary>
/// <param name="Unit">The word's unit, e.g. "A-3".</param>
/// <param name="MeaningHint">Uzbek → Korean only: what tells the word from another one with the same meaning (L-37).</param>
public sealed record WritingPromptDto(Guid LearningItemId, ReviewDirection Direction, string Prompt, string? PromptNote, string? Unit, string? MeaningHint = null);

/// <param name="Id">Chosen by the client once per answer and reused on retries (spec API01).</param>
public sealed record WritingCheckRequest(Guid Id, Guid LearningItemId, ReviewDirection Direction, string Answer);

public sealed record WritingResultDto(
    Guid Id,
    Domain.Writing.WritingVerdict Verdict,
    string Answer,
    string Expected,
    string? ExpectedNote,
    string? Note,
    bool AiChecked);

public sealed record WritingDirectionStatsDto(int Attempts, int Correct, int Spelling, int SameMeaning, int Wrong);

/// <summary>Writing results, kept apart from flash-card results (spec SRS06).</summary>
public sealed record WritingStatsDto(WritingDirectionStatsDto TypingUzbek, WritingDirectionStatsDto TypingKorean, int Last7Days);

/// <param name="Mistakes">Mistakes in the window (forgot on a card, wrong in a test, wrong in writing).</param>
/// <param name="Sources">Where they happened, e.g. "kartochka", "sinov", "og‘zaki", "yozma".</param>
/// <param name="Improving">The latest attempt after the last mistake was right.</param>
public sealed record MistakeWordDto(Guid LearningItemId, string Korean, string Uzbek, string? Unit, int Mistakes, DateTimeOffset LastMistakeAt, IReadOnlyList<string> Sources, bool Improving);

/// <summary>The mistakes notebook (D-41): weakest first, the ones already improving at the end.</summary>
public sealed record MistakesDto(int Days, IReadOnlyList<MistakeWordDto> Words, IReadOnlyList<MistakeListeningDto>? Listening = null);

/// <summary>A wrong listening answer (D-53): the question and the right option, linked to its set.</summary>
public sealed record MistakeListeningDto(Guid SetId, int Index, string QuestionKo, string RightOption, DateTimeOffset At);

/// <summary>How a word is used (D-46). <paramref name="TrapUz"/>: where a word-for-word Uzbek translation goes wrong.</summary>
public sealed record WordUsageDto(
    Guid LearningItemId,
    string Korean,
    string Uzbek,
    IReadOnlyList<UsagePair> Phrases,
    string? TrapUz,
    UsagePair? Example,
    int Questions,
    string? Warning);

/// <param name="Ready">Words whose usage is prepared; the rest still need the AI.</param>
public sealed record TableUsageDto(Guid UnitId, string Unit, int Total, int Ready, IReadOnlyList<WordUsageDto> Usages);

public sealed record UsageTableSummaryDto(Guid UnitId, string Unit, int Total, int Ready, int Questions);

/// <summary>A particle or verb ending chosen wrongly in the ladder's gaps (D-54).</summary>
/// <param name="Example">The sentence with the gap, as it was asked.</param>
/// <param name="Given">What the learner chose there.</param>
public sealed record WeakFormDto(string Form, string Kind, int Mistakes, int Total, string Example, string Given);

/// <summary>A kind of mistake in the learner's own sentences (D-54), with the latest example.</summary>
public sealed record WeakErrorTypeDto(string Label, int Count, string? ExampleQuote, string? ExampleHintUz);

public sealed record WeakTrapDto(string Wrong, string Right, string ExplanationUz);

/// <summary>
/// Weak points of the last days (D-54): what went wrong most, from every exercise, to practise next.
/// Words that are already improving are left out.
/// </summary>
public sealed record WeakPointsDto(
    int Days,
    IReadOnlyList<WeakFormDto> Forms,
    IReadOnlyList<WeakErrorTypeDto> ErrorTypes,
    IReadOnlyList<MistakeWordDto> Words,
    IReadOnlyList<WeakTrapDto> Traps,
    int ListeningWrong,
    int ListeningTotal);
