using KoreanAiTeacher.Domain.Listening;

namespace KoreanAiTeacher.Contracts;

/// <param name="Id">Chosen by the client, so a retry after a network error finds the same set.</param>
public sealed record CreateListeningRequest(Guid Id);

public sealed record ListeningSummaryDto(Guid Id, string Title, int Total, int Done, int Correct, DateTimeOffset CreatedAt);

/// <summary>What the learner sees after answering: the passage, its meaning and why.</summary>
public sealed record ListeningRevealDto(
    int Choice,
    int Answer,
    bool IsCorrect,
    IReadOnlyList<ListeningLine> Lines,
    string TranslationUz,
    string ExplanationUz,
    IReadOnlyList<ListeningWord> Words);

/// <summary>A question; the passage itself stays hidden (only heard) until <paramref name="Reveal"/> is set.</summary>
public sealed record ListeningItemDto(int Index, string QuestionKo, IReadOnlyList<string> Options, int Turns, ListeningRevealDto? Reveal);

public sealed record ListeningDto(Guid Id, string Title, IReadOnlyList<ListeningItemDto> Items, int Done, int Correct, string? Provider, DateTimeOffset CreatedAt);

public sealed record ListeningAnswerRequest(int Index, int Choice);
