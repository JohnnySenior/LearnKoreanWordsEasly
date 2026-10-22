using Lugat.Domain.Grammar;

namespace Lugat.Contracts;

public sealed record CreateGrammarImportRequest(int FromPage, int ToPage);

public sealed record GrammarImportDto(
    Guid Id,
    Guid SourceDocumentId,
    string FileName,
    int FromPage,
    int ToPage,
    int? DonePage,
    GrammarImportStatus Status,
    string? Error,
    int Items,
    int Approved,
    int Flagged,
    DateTimeOffset CreatedAt);

public sealed record GrammarItemDto(
    Guid Id,
    int PdfPage,
    int? Number,
    string? Section,
    string Form,
    string? Similar,
    string? MeaningKo,
    string MeaningUz,
    string? ConnectionUz,
    string? RestrictionUz,
    IReadOnlyList<GrammarExample> Examples,
    GrammarItemStatus Status,
    bool Flagged,
    string? Warning,
    uint Version);

/// <summary>PATCH /api/grammar/items/{id}. Null fields are left unchanged.</summary>
public sealed record UpdateGrammarItemRequest(
    string? Form,
    string? Similar,
    string? MeaningKo,
    string? MeaningUz,
    string? ConnectionUz,
    string? RestrictionUz,
    IReadOnlyList<GrammarExample>? Examples,
    GrammarItemStatus? Status,
    uint Version);

/// <summary>One row of the grammar list (D-26). Excluded items are not listed.</summary>
public sealed record GrammarListItemDto(
    Guid Id,
    int? Number,
    string? Section,
    string Form,
    string MeaningUz,
    GrammarItemStatus Status,
    bool Flagged,
    int Sentences,
    int GoodSentences,
    GrammarMastery Mastery,
    int GoodDays,
    int? SetNumber = null);

public sealed record GrammarGuideDto(
    string ExplanationUz,
    string? FormationUz,
    string? CommonMistakeUz,
    IReadOnlyList<GrammarExample> Examples,
    string Provider,
    string? Warning);

public sealed record GrammarSentenceDto(Guid Id, string Answer, string? Verdict, string? CorrectedText, DateTimeOffset CreatedAt);

/// <param name="Mastery">Spec GR03; <paramref name="GoodDays"/> counts towards <see cref="GrammarProgress.RequiredGoodDays"/>.</param>
/// <param name="Guide">Null until the AI has written it; the client then asks for it (POST .../guide).</param>
/// <param name="CanPractise">Only approved items go to practice (spec AC03).</param>
public sealed record GrammarDetailDto(
    GrammarItemDto Item,
    Guid ImportId,
    GrammarGuideDto? Guide,
    bool CanPractise,
    IReadOnlyList<GrammarSentenceDto> Recent,
    Guid? PreviousId,
    Guid? NextId,
    GrammarMastery Mastery,
    int GoodDays,
    IReadOnlyList<GrammarNoteDto> Notes,
    Guid? SetId = null,
    int? SetNumber = null);

public sealed record GrammarNoteDto(
    Guid Id,
    GrammarQuestionKind Kind,
    string? Question,
    string AnswerUz,
    IReadOnlyList<GrammarExample> Examples,
    string Provider,
    DateTimeOffset CreatedAt);

/// <param name="Id">Chosen by the client once per question and reused on retries (spec API01).</param>
/// <param name="Question">Required for <see cref="GrammarQuestionKind.Question"/>; ignored otherwise.</param>
public sealed record AskGrammarRequest(Guid Id, GrammarQuestionKind Kind, string? Question);

/// <summary>A grammar set ("to‘plam", D-28) with progress.</summary>
/// <param name="Started">Points with at least one checked sentence.</param>
/// <param name="TodayTarget">New points to start today (spread over the days until <paramref name="DueDate"/>).</param>
/// <param name="StartedToday">Points whose first sentence was written today.</param>
/// <param name="Overdue">The deadline has passed: points not yet "Mustaqil" need review.</param>
/// <param name="NextItemId">The point to open with "Boshlash": first not started, else first not independent.</param>
public sealed record GrammarSetDto(
    Guid Id,
    int Number,
    DateOnly? DueDate,
    int Total,
    int Started,
    int Independent,
    int TodayTarget,
    int StartedToday,
    bool Overdue,
    Guid? NextItemId,
    DateTimeOffset CreatedAt,
    uint Version);

public sealed record GrammarSetDetailDto(GrammarSetDto Set, IReadOnlyList<GrammarListItemDto> Items);

/// <param name="Current">The set being learned now: the newest one not finished and not overdue.</param>
/// <param name="ReviewDue">Points of overdue sets that are not "Mustaqil" yet.</param>
/// <param name="FreeItems">Approved points not in any set, to offer a new set.</param>
public sealed record GrammarTodayDto(GrammarSetDto? Current, int ReviewDue, Guid? ReviewNextItemId, int FreeItems);

public sealed record CreateGrammarSetRequest(IReadOnlyList<Guid> ItemIds, DateOnly? DueDate);

/// <summary>PATCH /api/grammar/sets/{id}. Null lists are left unchanged.</summary>
public sealed record UpdateGrammarSetRequest(DateOnly? DueDate, bool ClearDueDate, IReadOnlyList<Guid>? AddItemIds, IReadOnlyList<Guid>? RemoveItemIds, uint Version);
