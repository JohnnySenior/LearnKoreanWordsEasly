using System.Security.Cryptography;
using KoreanAiTeacher.Application.Abstractions;
using KoreanAiTeacher.Application.Vocabulary;
using KoreanAiTeacher.Contracts;
using KoreanAiTeacher.Domain.Imports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KoreanAiTeacher.Application.Imports;

/// <summary>
/// Vocabulary import: upload → background extraction → human review → approval (spec §4).
/// Every read and write is scoped to the owner; another user's IDs behave as "not found".
/// </summary>
public sealed class ImportService(
    IAppDbContext db,
    IFileStore files,
    IPdfLayoutReader pdf,
    VocabularyService vocabulary,
    IOptions<ImportOptions> options,
    TimeProvider time,
    ILogger<ImportService> logger)
{
    private static readonly byte[] PdfMagic = "%PDF-"u8.ToArray();

    private ImportOptions Options => options.Value;

    public async Task<UploadResult> UploadVocabularyAsync(Guid ownerId, string fileName, Stream content, CancellationToken ct)
    {
        using var buffer = await ReadLimitedAsync(content, Options.MaxFileBytes, ct);
        var bytes = buffer.ToArray();
        if (bytes.Length == 0)
        {
            throw new ValidationException("Fayl bo‘sh.");
        }

        if (!bytes.AsSpan().StartsWith(PdfMagic))
        {
            throw new ValidationException("Bu PDF fayl emas. Hozircha lug‘at faqat PDF ko‘rinishida qabul qilinadi.");
        }

        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var existing = await db.SourceDocuments
            .Where(d => d.OwnerId == ownerId && d.ContentSha256 == sha && d.Kind == SourceDocumentKind.Vocabulary)
            .Select(d => d.Id)
            .FirstOrDefaultAsync(ct);

        if (existing != Guid.Empty)
        {
            var document = await db.SourceDocuments.SingleAsync(d => d.Id == existing, ct);
            // A restored database may point at a lost volume. Re-upload repairs the original, never the progress.
            using var original = new MemoryStream(bytes, writable: false);
            await files.SaveAsync(document.StorageKey, original, ct);
            // Same file again: return the earlier import instead of duplicating words (spec IMP07, AC02).
            // A failed import is not a result worth keeping: uploading again is the retry, with a fresh job.
            var job = await db.ImportJobs
                .Where(j => j.SourceDocumentId == existing && j.Status != ImportStatus.Cancelled && j.Status != ImportStatus.Failed)
                .OrderByDescending(j => j.CreatedAt).Select(j => j.Id).FirstOrDefaultAsync(ct);
            if (job != Guid.Empty)
            {
                return new UploadResult(await GetJobAsync(ownerId, job, ct), Created: false);
            }
        }

        int pages;
        try
        {
            using var ms = new MemoryStream(bytes, writable: false);
            pages = pdf.CountPages(ms);
        }
        catch (PdfReadException ex)
        {
            throw new ValidationException(ex.UserMessage);
        }

        if (pages > Options.MaxPages)
        {
            throw new ValidationException($"PDF juda katta: {pages} sahifa. Ko‘pi bilan {Options.MaxPages} sahifa qabul qilinadi.");
        }

        var now = time.GetUtcNow();
        var documentId = existing != Guid.Empty ? existing : Guid.CreateVersion7(now);
        var storageKey = $"{ownerId:N}/{sha}.pdf";

        // Written again on a retry too, in case the earlier failure was a missing file.
        using (var ms = new MemoryStream(bytes, writable: false))
        {
            await files.SaveAsync(storageKey, ms, ct);
        }

        if (existing == Guid.Empty)
        {
            db.SourceDocuments.Add(new SourceDocument
            {
                Id = documentId,
                OwnerId = ownerId,
                Kind = SourceDocumentKind.Vocabulary,
                FileName = SafeFileName(fileName),
                ContentSha256 = sha,
                SizeBytes = bytes.Length,
                PageCount = pages,
                StorageKey = storageKey,
                UploadedAt = now,
            });
        }

        var jobEntity = new ImportJob
        {
            Id = Guid.CreateVersion7(now),
            OwnerId = ownerId,
            SourceDocumentId = documentId,
            Status = ImportStatus.Uploaded,
            CreatedAt = now,
        };
        db.ImportJobs.Add(jobEntity);
        await db.SaveChangesAsync(ct);

        return new UploadResult(await GetJobAsync(ownerId, jobEntity.Id, ct), Created: true);
    }

    /// <summary>
    /// Stores a grammar book whole (spec GR01, D-20). Its pages are extracted later, page range by page range,
    /// so nothing is parsed here. The same file again returns the stored document (IMP07).
    /// </summary>
    public async Task<(SourceDocumentDto Document, bool Created)> UploadGrammarBookAsync(Guid ownerId, string fileName, Stream content, CancellationToken ct)
    {
        using var buffer = await ReadLimitedAsync(content, Options.MaxFileBytes, ct);
        var bytes = buffer.ToArray();
        if (bytes.Length == 0)
        {
            throw new ValidationException("Fayl bo‘sh.");
        }

        if (!bytes.AsSpan().StartsWith(PdfMagic))
        {
            throw new ValidationException("Bu PDF fayl emas. Grammatika kitobi PDF ko‘rinishida qabul qilinadi.");
        }

        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var existing = await db.SourceDocuments
            .SingleOrDefaultAsync(d => d.OwnerId == ownerId && d.ContentSha256 == sha && d.Kind == SourceDocumentKind.Grammar, ct);
        if (existing is not null)
        {
            using var original = new MemoryStream(bytes, writable: false);
            await files.SaveAsync(existing.StorageKey, original, ct);
            return (ToDto(existing), false);
        }

        int pages;
        try
        {
            using var ms = new MemoryStream(bytes, writable: false);
            pages = pdf.CountPages(ms);
        }
        catch (PdfReadException ex)
        {
            throw new ValidationException(ex.UserMessage);
        }

        if (pages > Options.MaxPages)
        {
            throw new ValidationException($"PDF juda katta: {pages} sahifa. Ko‘pi bilan {Options.MaxPages} sahifa qabul qilinadi.");
        }

        var now = time.GetUtcNow();
        var storageKey = $"{ownerId:N}/{sha}.pdf";
        using (var ms = new MemoryStream(bytes, writable: false))
        {
            await files.SaveAsync(storageKey, ms, ct);
        }

        var document = new SourceDocument
        {
            Id = Guid.CreateVersion7(now),
            OwnerId = ownerId,
            Kind = SourceDocumentKind.Grammar,
            FileName = SafeFileName(fileName),
            ContentSha256 = sha,
            SizeBytes = bytes.Length,
            PageCount = pages,
            StorageKey = storageKey,
            UploadedAt = now,
        };
        db.SourceDocuments.Add(document);
        await db.SaveChangesAsync(ct);
        return (ToDto(document), true);
    }

    public async Task<IReadOnlyList<SourceDocumentDto>> ListDocumentsAsync(Guid ownerId, SourceDocumentKind kind, CancellationToken ct) =>
        (await db.SourceDocuments.Where(d => d.OwnerId == ownerId && d.Kind == kind).OrderByDescending(d => d.UploadedAt).ToListAsync(ct))
            .Select(ToDto).ToList();

    private static SourceDocumentDto ToDto(SourceDocument d) => new(d.Id, d.Kind, d.FileName, d.SizeBytes, d.PageCount, d.UploadedAt);

    /// <summary>Runs queued imports. Called by the background worker; safe to call repeatedly.</summary>
    public async Task<int> ProcessPendingAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var staleBefore = now - Options.StaleAfter;
        await db.ImportJobs
            .Where(j => j.Status == ImportStatus.Extracting && j.StartedAt < staleBefore)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, ImportStatus.Uploaded), ct);

        var processed = 0;
        while (!ct.IsCancellationRequested)
        {
            var job = await db.ImportJobs.Include(j => j.SourceDocument)
                .Where(j => j.Status == ImportStatus.Uploaded)
                .OrderBy(j => j.CreatedAt)
                .FirstOrDefaultAsync(ct);
            if (job is null)
            {
                break;
            }

            await ProcessAsync(job, ct);
            processed++;
        }

        return processed;
    }

    private async Task ProcessAsync(ImportJob job, CancellationToken ct)
    {
        job.Status = ImportStatus.Extracting;
        job.Attempts++;
        job.StartedAt = time.GetUtcNow();
        job.Error = null;
        await db.SaveChangesAsync(ct);

        try
        {
            IReadOnlyList<LayoutPage> pages;
            await using (var stream = await files.OpenReadAsync(job.SourceDocument!.StorageKey, ct)
                ?? throw new PdfReadException("Asl fayl topilmadi. Uni qayta yuklang."))
            {
                pages = pdf.ReadPages(stream);
            }

            var parsed = VocabularyLayoutParser.Parse(pages);
            if (parsed.Entries.Count == 0)
            {
                Fail(job, "PDFda so‘z jadvali topilmadi. Chapda koreyscha so‘z, o‘ngda tarjima bo‘lgan lug‘at kutilgan edi.");
                await db.SaveChangesAsync(ct);
                return;
            }

            // A retried job may have written rows before it died.
            await db.SourceEntries.Where(e => e.ImportJobId == job.Id).ExecuteDeleteAsync(ct);

            var now = time.GetUtcNow();
            foreach (var e in parsed.Entries)
            {
                db.SourceEntries.Add(new SourceEntry
                {
                    Id = Guid.CreateVersion7(now),
                    OwnerId = job.OwnerId,
                    ImportJobId = job.Id,
                    SourceDocumentId = job.SourceDocumentId,
                    PdfPage = e.PdfPage,
                    PrintedPage = e.PrintedPage,
                    TableNumber = e.TableNumber,
                    OrderInTable = e.OrderInTable,
                    OrderInDocument = e.OrderInDocument,
                    RawText = e.RawText,
                    OriginalKorean = e.Korean,
                    OriginalKoreanNote = e.KoreanNote,
                    OriginalUzbek = e.Uzbek,
                    Korean = e.Korean,
                    KoreanNote = e.KoreanNote,
                    Uzbek = e.Uzbek,
                    Warnings = e.Warnings,
                    Status = EntryStatus.NeedsReview,
                });
            }

            job.Status = ImportStatus.NeedsReview;
            job.ParserVersion = VocabularyLayoutParser.Version;
            job.FinishedAt = now;
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Import {JobId}: {Count} entries, skipped pages {Skipped}", job.Id, parsed.Entries.Count, parsed.SkippedPages);
        }
        catch (PdfReadException ex)
        {
            Fail(job, ex.UserMessage);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Import {JobId} failed on attempt {Attempt}", job.Id, job.Attempts);
            if (job.Attempts >= Options.MaxAttempts)
            {
                Fail(job, "Faylni qayta ishlashda xato yuz berdi. Keyinroq qayta yuklab ko‘ring.");
            }
            else
            {
                job.Status = ImportStatus.Uploaded;
            }

            await db.SaveChangesAsync(ct);
        }
    }

    private void Fail(ImportJob job, string message)
    {
        job.Status = ImportStatus.Failed;
        job.Error = message;
        job.FinishedAt = time.GetUtcNow();
    }

    public async Task<IReadOnlyList<ImportJobDto>> ListJobsAsync(Guid ownerId, CancellationToken ct) =>
        await Project(db.ImportJobs.Where(j => j.OwnerId == ownerId).OrderByDescending(j => j.CreatedAt)).ToListAsync(ct);

    public async Task<ImportJobDto> GetJobAsync(Guid ownerId, Guid jobId, CancellationToken ct) =>
        await Project(db.ImportJobs.Where(j => j.OwnerId == ownerId && j.Id == jobId)).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Import topilmadi.");

    private static IQueryable<ImportJobDto> Project(IQueryable<ImportJob> jobs) =>
        jobs.Select(j => new ImportJobDto(
            j.Id,
            j.SourceDocumentId,
            j.SourceDocument!.FileName,
            j.SourceDocument.PageCount,
            j.Status,
            j.Error,
            j.CreatedAt,
            j.FinishedAt,
            j.Entries.Count,
            j.Entries.Count(e => e.Status == EntryStatus.Approved),
            j.Entries.Count(e => e.Status == EntryStatus.Excluded),
            j.Entries.Count(e => e.Status == EntryStatus.NeedsReview),
            j.Entries.Count(e => e.Status == EntryStatus.NeedsReview && (e.Warnings & EntryWarningsExtensions.Blocking) != 0)));

    public async Task<IReadOnlyList<ImportTableDto>> ListTablesAsync(Guid ownerId, Guid jobId, CancellationToken ct)
    {
        await EnsureJobAsync(ownerId, jobId, ct);
        return await db.SourceEntries
            .Where(e => e.ImportJobId == jobId && e.OwnerId == ownerId)
            .GroupBy(e => e.TableNumber)
            .OrderBy(g => g.Key)
            .Select(g => new ImportTableDto(
                g.Key,
                g.Min(e => e.PdfPage),
                g.Min(e => e.PrintedPage),
                g.Count(),
                g.Count(e => e.Status == EntryStatus.Approved),
                g.Count(e => e.Status == EntryStatus.Excluded),
                g.Count(e => e.Status == EntryStatus.NeedsReview),
                g.Count(e => e.Status == EntryStatus.NeedsReview && (e.Warnings & EntryWarningsExtensions.Blocking) != 0)))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SourceEntryDto>> ListEntriesAsync(Guid ownerId, Guid jobId, int? table, bool flaggedOnly, CancellationToken ct)
    {
        await EnsureJobAsync(ownerId, jobId, ct);
        var query = db.SourceEntries.Where(e => e.ImportJobId == jobId && e.OwnerId == ownerId);
        if (table is { } t)
        {
            query = query.Where(e => e.TableNumber == t);
        }

        if (flaggedOnly)
        {
            query = query.Where(e => e.Warnings != EntryWarnings.None);
        }

        var entries = await query.OrderBy(e => e.OrderInDocument).ToListAsync(ct);
        return entries.Select(ToDto).ToList();
    }

    public async Task<SourceEntryDto> UpdateEntryAsync(Guid ownerId, Guid entryId, EntryPatch patch, CancellationToken ct)
    {
        var entry = await db.SourceEntries.SingleOrDefaultAsync(e => e.Id == entryId && e.OwnerId == ownerId, ct)
            ?? throw new NotFoundException("So‘z topilmadi.");

        // The client's version is what it last saw; EF compares it against xmin on save.
        db.SourceEntries.Entry(entry).Property(e => e.Version).OriginalValue = patch.Version;

        if (patch.Korean is not null)
        {
            var korean = patch.Korean.Trim();
            if (!korean.Any(ch => ch is >= '가' and <= '힣'))
            {
                throw new ValidationException("Koreyscha so‘z Hangul harflarida bo‘lishi kerak.");
            }

            entry.Korean = korean;
        }

        if (patch.KoreanNote is not null)
        {
            entry.KoreanNote = patch.KoreanNote.Trim() is { Length: > 0 } note ? note : null;
        }

        if (patch.Uzbek is not null)
        {
            entry.Uzbek = patch.Uzbek.Trim();
        }

        if (patch.Status is { } status)
        {
            entry.Status = status;
            entry.ReviewedAt = status == EntryStatus.NeedsReview ? null : time.GetUtcNow();
        }

        // Check the result, not the request: an edit to an already approved row must not leave it unusable.
        if (entry.Status == EntryStatus.Approved && entry.Uzbek.Length == 0)
        {
            throw new ValidationException("Tasdiqlangan so‘zning tarjimasi bo‘sh bo‘lishi mumkin emas.");
        }

        // The row, the import status and the practice cards change together or not at all.
        await using var tx = await db.BeginTransactionAsync(ct);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Bu so‘z boshqa oynada o‘zgartirilgan. Sahifani yangilab, qayta urinib ko‘ring.");
        }

        await RefreshJobStatusAsync(entry.ImportJobId, ct);
        await vocabulary.SyncAsync(ownerId, entry.ImportJobId, null, entry.Id, ct);
        await tx.CommitAsync(ct);
        return ToDto(entry);
    }

    /// <summary>
    /// Approves every undecided row of a table that has no blocking warning.
    /// Flagged rows stay for a person to decide (spec IMP05).
    /// </summary>
    public async Task<ApproveTableResult> ApproveTableAsync(Guid ownerId, Guid jobId, int table, CancellationToken ct)
    {
        await EnsureJobAsync(ownerId, jobId, ct);
        var rows = db.SourceEntries.Where(e => e.ImportJobId == jobId && e.OwnerId == ownerId && e.TableNumber == table);
        if (!await rows.AnyAsync(ct))
        {
            throw new NotFoundException("Jadval topilmadi.");
        }

        var now = time.GetUtcNow();
        await using var tx = await db.BeginTransactionAsync(ct);
        var approved = await rows
            .Where(e => e.Status == EntryStatus.NeedsReview && (e.Warnings & EntryWarningsExtensions.Blocking) == 0 && e.Uzbek != "")
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, EntryStatus.Approved).SetProperty(e => e.ReviewedAt, now), ct);

        var remaining = await rows.CountAsync(e => e.Status == EntryStatus.NeedsReview, ct);
        await RefreshJobStatusAsync(jobId, ct);
        await vocabulary.SyncAsync(ownerId, jobId, table, null, ct);
        await tx.CommitAsync(ct);
        return new ApproveTableResult(approved, remaining);
    }

    public async Task<(Stream Content, string FileName)> OpenOriginalAsync(Guid ownerId, Guid jobId, CancellationToken ct)
    {
        var doc = await db.ImportJobs.Where(j => j.Id == jobId && j.OwnerId == ownerId).Select(j => j.SourceDocument!).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Import topilmadi.");
        var stream = await files.OpenReadAsync(doc.StorageKey, ct) ?? throw new NotFoundException("Asl fayl topilmadi.");
        return (stream, doc.FileName);
    }

    private async Task RefreshJobStatusAsync(Guid jobId, CancellationToken ct)
    {
        var job = await db.ImportJobs.SingleAsync(j => j.Id == jobId, ct);
        if (job.Status is not (ImportStatus.NeedsReview or ImportStatus.Confirmed))
        {
            return;
        }

        var undecided = await db.SourceEntries.AnyAsync(e => e.ImportJobId == jobId && e.Status == EntryStatus.NeedsReview, ct);
        var status = undecided ? ImportStatus.NeedsReview : ImportStatus.Confirmed;
        if (job.Status != status)
        {
            job.Status = status;
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task EnsureJobAsync(Guid ownerId, Guid jobId, CancellationToken ct)
    {
        if (!await db.ImportJobs.AnyAsync(j => j.Id == jobId && j.OwnerId == ownerId, ct))
        {
            throw new NotFoundException("Import topilmadi.");
        }
    }

    private static SourceEntryDto ToDto(SourceEntry e) => new(
        e.Id,
        e.TableNumber,
        e.OrderInTable,
        e.PdfPage,
        e.PrintedPage,
        e.RawText,
        e.OriginalKorean,
        e.OriginalKoreanNote,
        e.OriginalUzbek,
        e.Korean,
        e.KoreanNote,
        e.Uzbek,
        e.Warnings,
        e.Warnings.IsBlocking(),
        e.Status,
        e.Version);

    private static string SafeFileName(string name)
    {
        var clean = Path.GetFileName(name ?? "").Trim();
        if (clean.Length == 0)
        {
            return "lugat.pdf";
        }

        return clean.Length > 200 ? clean[..200] : clean;
    }

    private static async Task<MemoryStream> ReadLimitedAsync(Stream content, long maxBytes, CancellationToken ct)
    {
        var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                await buffer.DisposeAsync();
                throw new PayloadTooLargeException($"Fayl juda katta. Ko‘pi bilan {maxBytes / 1024 / 1024} MB qabul qilinadi.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer;
    }
}

