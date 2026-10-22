using Lugat.Contracts;
using Lugat.Domain.Imports;

namespace Lugat.Application.Imports;

public sealed class ImportOptions
{
    public const string SectionName = "Imports";

    /// <summary>
    /// Spec IMP08 was 25 MiB. Raised to 100 MiB with Johnny's agreement (D-20): the 43 MB grammar book
    /// is kept whole, because its TOPIK test sections are wanted in the app later.
    /// </summary>
    public long MaxFileBytes { get; set; } = 100 * 1024 * 1024;

    /// <summary>Spec IMP08 was 300 pages; raised with the file size (D-20).</summary>
    public int MaxPages { get; set; } = 600;

    public int MaxAttempts { get; set; } = 3;

    /// <summary>An "Extracting" job older than this is assumed to have died with the process and is retried.</summary>
    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromMinutes(10);

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);
}

public sealed record UploadResult(ImportJobDto Job, bool Created);

/// <summary>Null fields are left unchanged.</summary>
public sealed record EntryPatch(string? Korean, string? KoreanNote, string? Uzbek, EntryStatus? Status, uint Version);
