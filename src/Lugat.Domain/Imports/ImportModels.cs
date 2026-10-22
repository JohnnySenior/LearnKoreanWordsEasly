namespace Lugat.Domain.Imports;

public enum SourceDocumentKind
{
    Vocabulary,
    Grammar,
}

/// <summary>An uploaded original file (spec SRC01). The file itself lives in file storage under <see cref="StorageKey"/>.</summary>
public sealed class SourceDocument
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public SourceDocumentKind Kind { get; set; }

    public string FileName { get; set; } = "";

    /// <summary>Lower-case hex SHA-256 of the file. Same owner + same hash = same document (spec IMP07).</summary>
    public string ContentSha256 { get; set; } = "";

    public long SizeBytes { get; set; }

    public int? PageCount { get; set; }

    public string StorageKey { get; set; } = "";

    public DateTimeOffset UploadedAt { get; set; }
}

/// <summary>Import lifecycle from spec IMP02.</summary>
public enum ImportStatus
{
    /// <summary>"Yuklandi".</summary>
    Uploaded,

    /// <summary>"Ajratilmoqda".</summary>
    Extracting,

    /// <summary>"Tekshirish kerak".</summary>
    NeedsReview,

    /// <summary>"Tasdiqlandi": no entry is left undecided.</summary>
    Confirmed,

    /// <summary>"Xatolik".</summary>
    Failed,

    /// <summary>"Bekor qilindi".</summary>
    Cancelled,
}

public sealed class ImportJob
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public Guid SourceDocumentId { get; set; }

    public SourceDocument? SourceDocument { get; set; }

    public ImportStatus Status { get; set; }

    public int Attempts { get; set; }

    /// <summary>User-readable reason when <see cref="Status"/> is <see cref="ImportStatus.Failed"/>.</summary>
    public string? Error { get; set; }

    public string? ParserVersion { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public List<SourceEntry> Entries { get; set; } = [];
}

/// <summary>Why an extracted row needs a human look (spec IMP04).</summary>
[Flags]
public enum EntryWarnings
{
    None = 0,

    /// <summary>The translation was assembled from several printed lines.</summary>
    MultiLineTranslation = 1 << 0,

    MissingTranslation = 1 << 1,

    /// <summary>Numbered senses do not run 1, 2, 3… (for example "2." without "1.").</summary>
    BrokenNumbering = 1 << 2,

    /// <summary>The same Korean headword already appeared earlier in this document.</summary>
    PossibleDuplicate = 1 << 3,

    /// <summary>A text fragment could not be attached to a headword with confidence.</summary>
    UnattachedFragment = 1 << 4,

    UnexpectedCharacters = 1 << 5,

    /// <summary>Several numbered meanings. Highlighted, but not a reason to block approval.</summary>
    MultipleSenses = 1 << 6,
}

public static class EntryWarningsExtensions
{
    /// <summary>Warnings that keep an entry out of bulk approval until a person decides (spec IMP05).</summary>
    public const EntryWarnings Blocking =
        EntryWarnings.MultiLineTranslation
        | EntryWarnings.MissingTranslation
        | EntryWarnings.BrokenNumbering
        | EntryWarnings.PossibleDuplicate
        | EntryWarnings.UnattachedFragment
        | EntryWarnings.UnexpectedCharacters;

    public static bool IsBlocking(this EntryWarnings warnings) => (warnings & Blocking) != 0;
}

public enum EntryStatus
{
    NeedsReview,
    Approved,
    Excluded,
}

/// <summary>
/// One extracted row, linked to its exact place in the source (spec SRC02).
/// The printed text is kept in <see cref="OriginalKorean"/>/<see cref="OriginalUzbek"/> and never overwritten (spec SRC03).
/// </summary>
public sealed class SourceEntry
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public Guid ImportJobId { get; set; }

    public Guid SourceDocumentId { get; set; }

    public int PdfPage { get; set; }

    /// <summary>Page number printed on the page, when one was found.</summary>
    public int? PrintedPage { get; set; }

    /// <summary>1-based ordinal of the word table (one table per word page, usually 30 rows).</summary>
    public int TableNumber { get; set; }

    public int OrderInTable { get; set; }

    public int OrderInDocument { get; set; }

    public string RawText { get; set; } = "";

    public string OriginalKorean { get; set; } = "";

    public string? OriginalKoreanNote { get; set; }

    public string OriginalUzbek { get; set; } = "";

    public string Korean { get; set; } = "";

    /// <summary>Printed extra next to the headword, e.g. the "(겨우)" in "간신히 (겨우)".</summary>
    public string? KoreanNote { get; set; }

    public string Uzbek { get; set; } = "";

    public EntryWarnings Warnings { get; set; }

    public EntryStatus Status { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>Optimistic concurrency token (Postgres xmin).</summary>
    public uint Version { get; set; }
}
