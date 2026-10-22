using KoreanAiTeacher.Domain.Imports;

namespace KoreanAiTeacher.Contracts;

public sealed record ImportJobDto(
    Guid Id,
    Guid SourceDocumentId,
    string FileName,
    int? PageCount,
    ImportStatus Status,
    string? Error,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FinishedAt,
    int TotalEntries,
    int Approved,
    int Excluded,
    int NeedsReview,
    int Flagged);

public sealed record ImportTableDto(
    int TableNumber,
    int PdfPage,
    int? PrintedPage,
    int Total,
    int Approved,
    int Excluded,
    int NeedsReview,
    int Flagged);

public sealed record SourceEntryDto(
    Guid Id,
    int TableNumber,
    int OrderInTable,
    int PdfPage,
    int? PrintedPage,
    string RawText,
    string OriginalKorean,
    string? OriginalKoreanNote,
    string OriginalUzbek,
    string Korean,
    string? KoreanNote,
    string Uzbek,
    EntryWarnings Warnings,
    bool Blocking,
    EntryStatus Status,
    uint Version);

public sealed record ApproveTableResult(int Approved, int StillNeedsReview);

/// <summary>PATCH /api/entries/{id}. Null fields are left unchanged; <see cref="Version"/> is the version last read.</summary>
public sealed record UpdateEntryRequest(string? Korean, string? KoreanNote, string? Uzbek, EntryStatus? Status, uint Version);

/// <summary>An uploaded book kept whole (e.g. the grammar book). Extraction of its pages is a separate step.</summary>
public sealed record SourceDocumentDto(Guid Id, SourceDocumentKind Kind, string FileName, long SizeBytes, int? PageCount, DateTimeOffset UploadedAt);
