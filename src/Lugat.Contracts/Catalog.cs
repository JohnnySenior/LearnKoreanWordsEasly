using Lugat.Domain.Catalog;

namespace Lugat.Contracts;

// Learner side (spec §11). Only published units are ever listed; the rest is a count ("Tez orada").

public sealed record LevelDto(CatalogLevel Level, string Title, int PublishedUnits, int ComingSoonUnits);

public enum UnitState
{
    /// <summary>Not open yet: its words are not available (spec §4.2).</summary>
    Locked,
    Open,
    Passed,
}

/// <param name="Label">"A-3".</param>
/// <param name="Started">Words with a card past "new" (spec §4.2 "12/25 boshlangan").</param>
/// <param name="Themes">The unit's most common theme tags, at most 3.</param>
/// <param name="CanSkip">The next locked unit: "Bilaman — testni topshirish" is offered (spec §4.5).</param>
/// <param name="Opened">The learner opened the unit (its table), L-42.</param>
/// <param name="Practiced">A practice round with its words is done, L-42.</param>
public sealed record UnitSummaryDto(
    Guid Id,
    CatalogLevel Level,
    int Number,
    string Label,
    int WordCount,
    UnitState State,
    int Started,
    int? BestScorePercent,
    IReadOnlyList<string> Themes,
    bool CanSkip = false,
    bool Opened = false,
    bool Practiced = false);

/// <param name="ComingSoon">Units of the level that are still being prepared.</param>
public sealed record LevelUnitsDto(CatalogLevel Level, string Title, IReadOnlyList<UnitSummaryDto> Units, int ComingSoon);

/// <param name="PosUz">The part of speech in Uzbek, e.g. "fe’l".</param>
/// <param name="LearningItemId">What the word's cards and practice refer to.</param>
/// <param name="Started">The learner has rated one of its cards.</param>
public sealed record CatalogWordDto(
    Guid Id,
    Guid LearningItemId,
    bool Started,
    int Order,
    string Word,
    string? Homonym,
    string Pos,
    string PosUz,
    string TranslationUz,
    string? Romanization,
    string? ExampleKo,
    string? ExampleUz,
    string? TrapUz,
    string? Theme,
    string Source);

/// <param name="CardsReady">The learner started the unit: its cards exist (spec §5.3).</param>
public sealed record UnitDto(UnitSummaryDto Unit, bool CardsReady, IReadOnlyList<CatalogWordDto> Words);

// Content work (spec §2.4, §10): editors and admins.

/// <param name="Unplaced">Studyable words in no unit yet: not split, or too few for a unit of their own.</param>
public sealed record CatalogLevelAdminDto(
    CatalogLevel Level,
    int Words,
    int Studyable,
    int Unplaced,
    int Units,
    int PublishedUnits,
    int Reviewed);

/// <param name="Problems">Rows that were skipped and why (the first ones).</param>
public sealed record CatalogImportResultDto(
    int Rows,
    int Created,
    int Updated,
    int Unchanged,
    int Skipped,
    IReadOnlyList<string> Problems,
    IReadOnlyList<CatalogLevelAdminDto> Levels);

public sealed record UnitAdminSummaryDto(
    Guid Id,
    CatalogLevel Level,
    int Number,
    UnitStatus Status,
    int Words,
    int Drafted,
    int Reviewed,
    DateTimeOffset? PublishedAt);

public sealed record CatalogQuestionDto(string Uzbek, string Sentence, string Answer, IReadOnlyList<string> Wrong, string ExplanationUz);

/// <param name="ReviewedBy">Display name of the person who checked the word.</param>
/// <param name="Version">Send back when saving; a changed word answers 409.</param>
/// <param name="SameMeaning">Other catalog words with one of this word's Uzbek meanings (안 / 아니다 "emas", L-37).</param>
public sealed record CatalogWordAdminDto(
    Guid Id,
    int? Order,
    string Word,
    string? Homonym,
    string Pos,
    string PosUz,
    string? Gloss,
    int? FreqRank,
    string? TranslationUz,
    string? Romanization,
    string? ExampleKo,
    string? ExampleUz,
    string? TrapUz,
    string? Theme,
    CatalogWordStatus Status,
    int ContentVersion,
    string? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    string? AiProvider,
    IReadOnlyList<CatalogQuestionDto> Questions,
    uint Version,
    IReadOnlyList<string>? SameMeaning = null,
    string? TrapCue = null,
    string? TrapWrong = null,
    string? TrapRight = null,
    bool TrapScenePending = false,
    bool TrapSceneAsked = false);

/// <param name="Remaining">Words still waiting for an AI draft.</param>
/// <param name="ScenesRemaining">Words with a trap note the AI has not yet been asked to act out (L-52).</param>
public sealed record UnitAdminDto(UnitAdminSummaryDto Unit, IReadOnlyList<CatalogWordAdminDto> Words, int Remaining, int ScenesRemaining = 0);

/// <summary>All content fields; the official ones (word, homonym, part of speech, level, rank) cannot be changed.</summary>
public sealed record UpdateCatalogWordRequest(
    string? TranslationUz,
    string? Romanization,
    string? ExampleKo,
    string? ExampleUz,
    string? TrapUz,
    string? Theme,
    IReadOnlyList<CatalogQuestionDto>? Questions,
    uint Version,
    string? TrapCue = null,
    string? TrapWrong = null,
    string? TrapRight = null);

public sealed record ReviewCatalogWordRequest(uint Version);
