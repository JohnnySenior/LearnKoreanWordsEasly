using KoreanAiTeacher.Application.Abstractions;
using KoreanAiTeacher.Application.Text;
using KoreanAiTeacher.Domain.Imports;
using KoreanAiTeacher.Domain.Learning;
using KoreanAiTeacher.Domain.Vocabulary;
using Microsoft.EntityFrameworkCore;

namespace KoreanAiTeacher.Application.Vocabulary;

/// <summary>
/// Keeps study material in step with reviewed source rows: an approved row becomes a word sense with one card per
/// direction (spec SRS01); a row taken back out deactivates the sense but keeps its cards and history (spec DATA02).
/// Only approved rows ever reach practice (spec IMP05).
/// </summary>
public sealed class VocabularyService(IAppDbContext db, TimeProvider time)
{
    private static readonly ReviewDirection[] Directions = [ReviewDirection.KoreanToUzbek, ReviewDirection.UzbekToKorean];

    /// <summary>Syncs the given rows of one import. Pass <paramref name="entryId"/> or <paramref name="table"/> to narrow it down.</summary>
    public async Task SyncAsync(Guid ownerId, Guid jobId, int? table, Guid? entryId, CancellationToken ct)
    {
        var query = db.SourceEntries.Where(e => e.OwnerId == ownerId && e.ImportJobId == jobId);
        if (table is { } t)
        {
            query = query.Where(e => e.TableNumber == t);
        }

        if (entryId is { } id)
        {
            query = query.Where(e => e.Id == id);
        }

        var entries = await query.ToListAsync(ct);
        if (entries.Count == 0)
        {
            return;
        }

        var ids = entries.Select(e => e.Id).ToList();
        var senses = await db.WordSenses.Where(s => ids.Contains(s.SourceEntryId)).ToDictionaryAsync(s => s.SourceEntryId, ct);
        var now = time.GetUtcNow();
        var created = new List<WordSense>();

        foreach (var entry in entries)
        {
            senses.TryGetValue(entry.Id, out var sense);
            if (entry.Status != EntryStatus.Approved)
            {
                if (sense is { IsActive: true })
                {
                    sense.IsActive = false;
                    sense.UpdatedAt = now;
                }

                continue;
            }

            if (sense is null)
            {
                sense = new WordSense
                {
                    Id = Guid.CreateVersion7(now),
                    OwnerId = ownerId,
                    SourceEntryId = entry.Id,
                    SourceDocumentId = entry.SourceDocumentId,
                    TableNumber = entry.TableNumber,
                    OrderInDocument = entry.OrderInDocument,
                    Korean = entry.Korean,
                    KoreanNote = entry.KoreanNote,
                    Uzbek = entry.Uzbek,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                db.WordSenses.Add(sense);
                created.Add(sense);
                continue;
            }

            var changed = TextNormalizer.Key(sense.Korean) != TextNormalizer.Key(entry.Korean)
                || TextNormalizer.Key(sense.Uzbek) != TextNormalizer.Key(entry.Uzbek)
                || TextNormalizer.Key(sense.KoreanNote) != TextNormalizer.Key(entry.KoreanNote);
            if (changed)
            {
                sense.Korean = entry.Korean;
                sense.KoreanNote = entry.KoreanNote;
                sense.Uzbek = entry.Uzbek;
                sense.ContentVersion++;
            }

            if (changed || !sense.IsActive)
            {
                sense.IsActive = true;
                sense.UpdatedAt = now;
            }
        }

        foreach (var sense in created)
        {
            foreach (var direction in Directions)
            {
                db.Cards.Add(new Card
                {
                    Id = Guid.CreateVersion7(now),
                    OwnerId = ownerId,
                    WordSenseId = sense.Id,
                    Direction = direction,
                    Phase = LearningPhase.New,
                });
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
