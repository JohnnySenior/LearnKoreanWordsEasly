using Lugat.Domain.Catalog;

namespace Lugat.Contracts;

// Learner side (spec §11). Only published units are ever listed; the rest is a count ("Tez orada").

public sealed record LevelDto(CatalogLevel Level, string Title, int PublishedUnits, int ComingSoonUnits);

public sealed record UnitSummaryDto(Guid Id, CatalogLevel Level, int Number, int WordCount);

/// <param name="ComingSoon">Units of the level that are still being prepared.</param>
public sealed record LevelUnitsDto(CatalogLevel Level, string Title, IReadOnlyList<UnitSummaryDto> Units, int ComingSoon);

/// <param name="PosUz">The part of speech in Uzbek, e.g. "fe’l".</param>
public sealed record CatalogWordDto(
    Guid Id,
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

public sealed record UnitDto(Guid Id, CatalogLevel Level, int Number, IReadOnlyList<CatalogWordDto> Words);

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
public sealed record CatalogWordAdminDto(
    Guid Id,
    int? Order,
    string Word,
    string? Homonym,
    string Pos,
    string PosUz,
    string? Gloss,
    int FreqRank,
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
    uint Version);

/// <param name="Remaining">Words still waiting for an AI draft.</param>
public sealed record UnitAdminDto(UnitAdminSummaryDto Unit, IReadOnlyList<CatalogWordAdminDto> Words, int Remaining);

/// <summary>All content fields; the official ones (word, homonym, part of speech, level, rank) cannot be changed.</summary>
public sealed record UpdateCatalogWordRequest(
    string? TranslationUz,
    string? Romanization,
    string? ExampleKo,
    string? ExampleUz,
    string? TrapUz,
    string? Theme,
    IReadOnlyList<CatalogQuestionDto>? Questions,
    uint Version);

public sealed record ReviewCatalogWordRequest(uint Version);
