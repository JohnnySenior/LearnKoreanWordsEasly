using Lugat.Domain.Ai;

namespace Lugat.Contracts;

public sealed record AiProviderStatusDto(AiProviderKind Kind, string Label, bool Configured, string? Model);

/// <param name="Chosen">The learner's choice; null = server default.</param>
/// <param name="Effective">The service that will actually be tried first; null when no key is configured.</param>
public sealed record AiStatusDto(
    IReadOnlyList<AiProviderStatusDto> Providers,
    AiProviderKind? Chosen,
    AiProviderKind Default,
    AiProviderKind? Effective,
    int CallsToday,
    int DailyLimit);

public sealed record ChooseAiProviderRequest(AiProviderKind? Provider);

public sealed record AiPingResult(bool Ok, AiProviderKind Provider, string? Model, int? LatencyMs, string Message);

/// <summary>A word to build a sentence with (spec PR05).</summary>
public sealed record SentenceTargetDto(Guid WordSenseId, string Korean, string? KoreanNote, string Uzbek, int TableNumber);

/// <param name="Id">Chosen by the client once per attempt and reused on retries (spec API01).</param>
/// <param name="GrammarItemId">Practise a grammar point instead of a word (D-26).</param>
public sealed record SentenceCheckRequest(Guid Id, Guid? WordSenseId, string Answer, Guid? GrammarItemId = null);

/// <summary>Spec AI01 structure, validated on the server.</summary>
/// <param name="Verdict">correct, mostly_correct, incorrect, task_not_done.</param>
/// <param name="ErrorType">none, grammar, spelling, meaning, word_choice, style, naturalness, target_missing.</param>
/// <param name="TargetUsage">used_correctly, used_incorrectly, not_used.</param>
/// <param name="Assisted">The sentence is a correct variant the app had already shown: accepted, but not counted as
/// the learner's own use (D-52).</param>
public sealed record SentenceCheckResult(
    string Verdict,
    string ErrorType,
    string HintUz,
    string ExplanationUz,
    string? CorrectedText,
    IReadOnlyList<string> Alternatives,
    string TargetUsage,
    double Confidence,
    IReadOnlyList<string> SourceRefs,
    IReadOnlyList<SentenceIssue>? Issues = null,
    bool Assisted = false);

/// <summary>
/// One problem in the learner's sentence (D-52): all of them are listed at once, not one per attempt.
/// <paramref name="Severity"/> "error" must be fixed; "suggestion" is an optional, more natural wording.
/// </summary>
/// <param name="Quote">The learner's words the problem is in, exactly as written.</param>
public sealed record SentenceIssue(string Quote, string Type, string Severity, string HintUz);

public sealed record SentenceCheckDto(
    Guid Id,
    Guid? WordSenseId,
    string Target,
    string Answer,
    AiCheckStatus Status,
    SentenceCheckResult? Result,
    string? Provider,
    string? Message,
    Guid? GrammarItemId = null);

/// <summary>One word of a pronunciation assessment (D-32). <paramref name="ErrorType"/>: None, Mispronunciation, Omission, Insertion.</summary>
public sealed record PronunciationWordDto(string Word, double? Accuracy, string ErrorType);

/// <summary>What the learner said (spec AU03) and, with a reference text, how it was pronounced (AU04).</summary>
/// <param name="Recognized">False when no speech was recognized; <paramref name="Message"/> says why.</param>
public sealed record SpeakingResultDto(
    bool Recognized,
    string RecognizedText,
    double? PronScore,
    double? Accuracy,
    double? Fluency,
    double? Completeness,
    IReadOnlyList<PronunciationWordDto> Words,
    string? Message);


/// <summary>Shared UTC calendar-month estimate. Unknown-price calls are reported explicitly, never counted as free.</summary>
public sealed record AiBudgetDto(decimal EstimatedUsd, decimal LimitUsd, bool Warning, int UnknownPriceCalls, int PendingCalls, DateTimeOffset MonthStart);
