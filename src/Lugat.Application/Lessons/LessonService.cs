using Lugat.Application.Abstractions;
using Lugat.Contracts;
using Lugat.Domain.Imports;
using Lugat.Domain.Lessons;
using Microsoft.EntityFrameworkCore;

namespace Lugat.Application.Lessons;

/// <summary>Lesson assignments: what the teacher will test and when (spec §5 PLAN01, PLAN02, PLAN05).</summary>
public sealed class LessonService(IAppDbContext db, TimeProvider time)
{
    public async Task<IReadOnlyList<AssignmentDto>> ListAsync(Guid ownerId, CancellationToken ct)
    {
        var assignments = await db.LessonAssignments.Where(a => a.OwnerId == ownerId)
            .OrderBy(a => a.Status).ThenBy(a => a.DueAt).ToListAsync(ct);
        var result = new List<AssignmentDto>();
        foreach (var a in assignments)
        {
            result.Add(await ToDtoAsync(a, ct));
        }

        return result;
    }

    /// <summary>All Planned lessons, nearest first. New words come from these, the nearest lesson first.</summary>
    public async Task<List<LessonAssignment>> PlannedLessonsAsync(Guid ownerId, CancellationToken ct) =>
        await db.LessonAssignments.Where(a => a.OwnerId == ownerId && a.Status == AssignmentStatus.Planned)
            .OrderBy(a => a.DueAt).ToListAsync(ct);

    /// <summary>
    /// Proposes the learner's number of tables after the last assigned one and the next lesson-day 23:00 slot
    /// after the last lesson (D-43; default two tables, Tue/Thu/Sat).
    /// Nothing is created until the learner confirms (spec PLAN01: "Shu darsga belgilash").
    /// </summary>
    public async Task<AssignmentSuggestionDto?> SuggestAsync(Guid ownerId, CancellationToken ct)
    {
        var user = await db.Users.SingleAsync(u => u.Id == ownerId, ct);
        var zone = user.Zone();

        var document = await db.ImportJobs
            .Where(j => j.OwnerId == ownerId && j.SourceDocument!.Kind == SourceDocumentKind.Vocabulary
                && (j.Status == ImportStatus.NeedsReview || j.Status == ImportStatus.Confirmed))
            .OrderByDescending(j => j.CreatedAt)
            .Select(j => new { j.SourceDocumentId, j.SourceDocument!.FileName, JobId = j.Id })
            .FirstOrDefaultAsync(ct);
        if (document is null)
        {
            return null;
        }

        var tables = await db.SourceEntries.Where(e => e.ImportJobId == document.JobId)
            .Select(e => e.TableNumber).Distinct().OrderBy(t => t).ToListAsync(ct);
        var assigned = await db.LessonAssignments
            .Where(a => a.OwnerId == ownerId && a.SourceDocumentId == document.SourceDocumentId)
            .Select(a => new { a.Tables, a.DueAt }).ToListAsync(ct);

        var taken = assigned.SelectMany(a => a.Tables).ToHashSet();
        var lastTaken = taken.Count == 0 ? 0 : taken.Max();
        var next = tables.Where(t => t > lastTaken && !taken.Contains(t)).Take(Math.Clamp(user.TablesPerLesson, 1, LessonCalendar.MaxTablesPerLesson)).ToArray();
        if (next.Length == 0)
        {
            return null;
        }

        var now = time.GetUtcNow();
        var after = assigned.Count == 0 ? now : new[] { now, assigned.Max(a => a.DueAt) }.Max();
        var dueAt = LessonCalendar.NextSlot(after, zone, user.LessonWeekdays());
        var (words, approved) = await CountWordsAsync(document.SourceDocumentId, next, ct);

        return new AssignmentSuggestionDto(document.SourceDocumentId, document.FileName, next, dueAt, words, approved);
    }

    /// <summary>
    /// After the learner changes their lesson plan (D-43), redistributes the tables of the Planned lessons that are
    /// not due yet: the new number of tables per lesson on the new lesson days, starting after the last lesson that
    /// stays as it was. Lessons already due or handed in are never touched. Returns how many lessons were rebuilt.
    /// </summary>
    public async Task<int> ReplanAsync(Guid ownerId, CancellationToken ct)
    {
        var user = await db.Users.SingleAsync(u => u.Id == ownerId, ct);
        var zone = user.Zone();
        var perLesson = Math.Clamp(user.TablesPerLesson, 1, LessonCalendar.MaxTablesPerLesson);
        var days = user.LessonWeekdays();
        var now = time.GetUtcNow();

        var all = await db.LessonAssignments.Where(a => a.OwnerId == ownerId).OrderBy(a => a.DueAt).ToListAsync(ct);
        var future = all.Where(a => a.Status == AssignmentStatus.Planned && a.DueAt > now).ToList();
        if (future.Count == 0)
        {
            return 0;
        }

        var kept = all.Except(future).Select(a => a.DueAt).Append(now).Max();
        var changed = 0;
        foreach (var lessons in future.GroupBy(a => a.SourceDocumentId))
        {
            var rows = lessons.ToList();
            var chunks = rows.SelectMany(a => a.Tables).Distinct().Order().Chunk(perLesson).ToList();
            var slot = kept;
            for (var i = 0; i < chunks.Count; i++)
            {
                slot = LessonCalendar.NextSlot(slot, zone, days);
                var a = i < rows.Count ? rows[i] : NewLesson(ownerId, lessons.Key);
                if (i >= rows.Count)
                {
                    db.LessonAssignments.Add(a);
                }

                if (string.IsNullOrEmpty(a.Title) || a.Title == DefaultTitle(a.Tables))
                {
                    a.Title = DefaultTitle(chunks[i]);
                }

                changed += a.DueAt != slot || !a.Tables.SequenceEqual(chunks[i]) ? 1 : 0;
                a.Tables = chunks[i];
                a.DueAt = slot;
            }

            foreach (var extra in rows.Skip(chunks.Count))
            {
                db.LessonAssignments.Remove(extra);
                changed++;
            }

            kept = slot;
        }

        await db.SaveChangesAsync(ct);
        return changed;
    }

    private LessonAssignment NewLesson(Guid ownerId, Guid documentId) => new()
    {
        Id = Guid.CreateVersion7(time.GetUtcNow()),
        OwnerId = ownerId,
        SourceDocumentId = documentId,
        Status = AssignmentStatus.Planned,
        CreatedAt = time.GetUtcNow(),
    };

    public async Task<AssignmentDto> CreateAsync(Guid ownerId, CreateAssignmentRequest request, CancellationToken ct)
    {
        var tables = await ValidateAsync(ownerId, request.SourceDocumentId, request.Tables, ct);
        var assignment = new LessonAssignment
        {
            Id = Guid.CreateVersion7(time.GetUtcNow()),
            OwnerId = ownerId,
            SourceDocumentId = request.SourceDocumentId,
            Tables = tables,
            DueAt = request.DueAt.ToUniversalTime(),
            Title = string.IsNullOrWhiteSpace(request.Title) ? DefaultTitle(tables) : request.Title.Trim(),
            Status = AssignmentStatus.Planned,
            CreatedAt = time.GetUtcNow(),
        };
        db.LessonAssignments.Add(assignment);
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(assignment, ct);
    }

    /// <summary>Changing a date or tables never touches review history (spec §12 LessonAssignment).</summary>
    public async Task<AssignmentDto> UpdateAsync(Guid ownerId, Guid id, UpdateAssignmentRequest request, CancellationToken ct)
    {
        var assignment = await db.LessonAssignments.SingleOrDefaultAsync(a => a.Id == id && a.OwnerId == ownerId, ct)
            ?? throw new NotFoundException("Dars topilmadi.");

        if (request.Tables is not null)
        {
            assignment.Tables = await ValidateAsync(ownerId, assignment.SourceDocumentId, request.Tables, ct);
        }

        if (request.DueAt is { } dueAt)
        {
            assignment.DueAt = dueAt.ToUniversalTime();
        }

        if (request.Status is { } status)
        {
            assignment.Status = status;
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            assignment.Title = request.Title.Trim();
        }

        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(assignment, ct);
    }

    public async Task<AssignmentDto> ToDtoAsync(LessonAssignment a, CancellationToken ct)
    {
        var (words, approved) = await CountWordsAsync(a.SourceDocumentId, a.Tables, ct);
        var newWords = await db.WordSenses
            .Where(s => s.OwnerId == a.OwnerId && s.IsActive && s.SourceDocumentId == a.SourceDocumentId && a.Tables.Contains(s.TableNumber))
            .CountAsync(s => db.Cards.Any(c => c.WordSenseId == s.Id && c.Phase == Domain.Learning.LearningPhase.New), ct);
        return new AssignmentDto(a.Id, a.SourceDocumentId, a.Title, a.Tables, a.DueAt, a.Status, words, approved, newWords);
    }

    private async Task<(int Words, int Approved)> CountWordsAsync(Guid documentId, int[] tables, CancellationToken ct)
    {
        var job = await db.ImportJobs.Where(j => j.SourceDocumentId == documentId && j.Status != ImportStatus.Cancelled)
            .OrderByDescending(j => j.CreatedAt).Select(j => j.Id).FirstOrDefaultAsync(ct);
        var rows = db.SourceEntries.Where(e => e.ImportJobId == job && tables.Contains(e.TableNumber));
        return (await rows.CountAsync(ct), await rows.CountAsync(e => e.Status == EntryStatus.Approved, ct));
    }

    private async Task<int[]> ValidateAsync(Guid ownerId, Guid documentId, int[] tables, CancellationToken ct)
    {
        if (!await db.SourceDocuments.AnyAsync(d => d.Id == documentId && d.OwnerId == ownerId, ct))
        {
            throw new NotFoundException("Kitob topilmadi.");
        }

        var clean = tables.Where(t => t > 0).Distinct().Order().ToArray();
        if (clean.Length == 0)
        {
            throw new ValidationException("Kamida bitta jadval tanlang.");
        }

        return clean;
    }

    private static string DefaultTitle(int[] tables) =>
        tables.Length == 1 ? $"{tables[0]}-jadval" : $"{tables[0]}–{tables[^1]}-jadvallar";
}
