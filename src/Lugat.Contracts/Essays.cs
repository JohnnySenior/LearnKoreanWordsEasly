using Lugat.Domain.Essays;

namespace Lugat.Contracts;

/// <param name="Id">Chosen by the client, so a double tap never makes two attempts (API01).</param>
/// <param name="InstructionKo">Own question: the instruction; empty = the usual one for the task.</param>
/// <param name="Text">Own question: the text, data or topic, as on the paper.</param>
public sealed record CreateEssayRequest(Guid Id, EssayTask Task, EssaySource Source, string? InstructionKo = null, string? Text = null);

/// <param name="Answers">51–52: the ㉠ and ㉡ answers; 53–54: one text.</param>
public sealed record SubmitEssayRequest(IReadOnlyList<string> Answers);

/// <param name="Id">The new attempt's id, chosen by the client.</param>
public sealed record RetryEssayRequest(Guid Id);

/// <param name="ModelAnswers">Only after checking, and only for AI-made questions.</param>
public sealed record EssayDto(
    Guid Id,
    EssayTask Task,
    EssaySource Source,
    string InstructionKo,
    string Text,
    IReadOnlyList<string> Answers,
    EssayStatus Status,
    int? MinChars,
    int? MaxChars,
    int MaxScore,
    EssayFeedback? Feedback,
    IReadOnlyList<string> ModelAnswers,
    DateTimeOffset CreatedAt,
    string? Provider,
    uint Version = 0);

public sealed record EssaySummaryDto(Guid Id, EssayTask Task, EssaySource Source, string Preview, EssayStatus Status, int? Score, int MaxScore, DateTimeOffset CreatedAt);


public sealed record SaveEssayDraftRequest(IReadOnlyList<string> Answers, uint Version);
