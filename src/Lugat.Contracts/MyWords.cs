using Lugat.Domain.Vocabulary;

namespace Lugat.Contracts;

// "Mening so‘zlarim" (L-51): words the learner meets outside the course, from the catalog first, else written by AI.

public sealed record MyWordLookupRequest(string Text);

/// <param name="Unit">The catalog unit the word is in ("A-17"), when it is published.</param>
/// <param name="AlreadyAdded">The learner has this word in "Mening so‘zlarim" already.</param>
public sealed record MyWordCandidateDto(
    string Korean,
    string Uzbek,
    string? Romanization,
    string? ExampleKo,
    string? ExampleUz,
    string? TrapUz,
    PersonalWordSource Source,
    Guid? CatalogWordId,
    string? Unit,
    bool AlreadyAdded,
    TrapSceneDto? TrapScene = null);

/// <param name="AiLeftToday">Free AI look-ups left today; null with Premium (no limit).</param>
public sealed record MyWordLookupDto(IReadOnlyList<MyWordCandidateDto> Candidates, bool UsedAi, int? AiLeftToday);

/// <param name="Id">Chosen by the page once; adding again returns the same word (API01).</param>
public sealed record AddMyWordRequest(
    Guid Id,
    string Korean,
    string Uzbek,
    string? Romanization,
    string? ExampleKo,
    string? ExampleUz,
    string? TrapUz,
    PersonalWordSource Source,
    Guid? CatalogWordId,
    Guid? DeckId,
    TrapSceneDto? TrapScene = null);

public enum MyWordState
{
    New,
    Learning,
    Learned,
}

/// <param name="IntroduceOn">The day its new cards join the daily review (10 own words a day).</param>
public sealed record MyWordDto(
    Guid Id,
    Guid LearningItemId,
    string Korean,
    string Uzbek,
    string? Romanization,
    string? ExampleKo,
    string? ExampleUz,
    string? TrapUz,
    PersonalWordSource Source,
    Guid? DeckId,
    MyWordState State,
    DateTimeOffset CreatedAt,
    DateOnly IntroduceOn,
    Guid? GroupDeckId = null,
    bool FromGroupDeck = false);

public sealed record WordDeckDto(Guid Id, string Name, int Words);

/// <param name="Unsorted">Words in no deck ("Umumiy").</param>
public sealed record MyWordsDto(IReadOnlyList<MyWordDto> Words, IReadOnlyList<WordDeckDto> Decks, int Unsorted, int? AiLeftToday,
    IReadOnlyList<GroupDeckOptionDto>? GroupDecks = null);

/// <summary>A teacher's deck the learner practises (L-67): read-only for the learner. <paramref name="Key"/> is "guruh:{id}".</summary>
/// <param name="Learned">Words of the deck the learner has learned (both cards mastered, L-68).</param>
public sealed record GroupDeckOptionDto(string Key, string Name, string GroupName, int Words, int Learned = 0, Guid Id = default, string? TeacherName = null);

/// <summary>The learner's view of one teacher's deck (L-68): its words with the learner's state, and the last test of it.</summary>
public sealed record GroupDeckLearnerDto(
    Guid Id,
    string Key,
    string Name,
    string GroupName,
    string? TeacherName,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<MyWordDto> Words,
    int Learned,
    int Learning,
    int New,
    GroupDeckTestDto? LastTest);

/// <param name="Correct">Right answers out of <paramref name="Questions"/> (technical errors not counted).</param>
public sealed record GroupDeckTestDto(Guid TestId, int Correct, int Questions, DateTimeOffset FinishedAt);

public sealed record DeckNameRequest(string Name);

public sealed record MoveMyWordRequest(Guid? DeckId);
