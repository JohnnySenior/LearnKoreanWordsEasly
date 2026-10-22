using Lugat.Application.Abstractions;
using Lugat.Application.Text;
using Lugat.Domain.Learning;
using Lugat.Domain.Vocabulary;
using Microsoft.EntityFrameworkCore;

namespace Lugat.Application.Vocabulary;

/// <summary>A word to add to a learner's vocabulary: its table and place in the list, most frequent first.</summary>
public sealed record NewWord(int TableNumber, int Order, string Korean, string? KoreanNote, string Uzbek);

/// <summary>
/// Keeps a learner's study words: a new word becomes a word sense with one card per direction (spec SRS01); a word
/// taken out deactivates the sense but keeps its cards and history (spec DATA02); edited text raises the content
/// version without resetting the schedule (D-13). Interim until the shared catalog (spec §5, stages 2–3).
/// </summary>
public sealed class VocabularyService(IAppDbContext db, TimeProvider time)
{
    private static readonly ReviewDirection[] Directions = [ReviewDirection.KoreanToUzbek, ReviewDirection.UzbekToKorean];

    /// <summary>Adds the words that are not there yet (same table and place), with their two cards. Idempotent.</summary>
    public async Task<IReadOnlyList<WordSense>> AddAsync(Guid ownerId, IReadOnlyList<NewWord> words, CancellationToken ct)
    {
        var tables = words.Select(w => w.TableNumber).Distinct().ToList();
        var existing = (await db.WordSenses.Where(s => s.OwnerId == ownerId && tables.Contains(s.TableNumber))
                .Select(s => new { s.TableNumber, s.Order }).ToListAsync(ct))
            .Select(s => (s.TableNumber, s.Order)).ToHashSet();
        var now = time.GetUtcNow();
        var created = new List<WordSense>();
        foreach (var word in words.Where(w => existing.Add((w.TableNumber, w.Order))))
        {
            var sense = new WordSense
            {
                Id = Guid.CreateVersion7(now),
                OwnerId = ownerId,
                TableNumber = word.TableNumber,
                Order = word.Order,
                Korean = word.Korean.Trim(),
                KoreanNote = string.IsNullOrWhiteSpace(word.KoreanNote) ? null : word.KoreanNote.Trim(),
                Uzbek = word.Uzbek.Trim(),
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.WordSenses.Add(sense);
            created.Add(sense);
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
        return created;
    }

    /// <summary>Takes a word out of practice or puts it back; cards and history stay (spec DATA02).</summary>
    public async Task SetActiveAsync(Guid ownerId, Guid senseId, bool active, CancellationToken ct)
    {
        var sense = await FindAsync(ownerId, senseId, ct);
        if (sense.IsActive != active)
        {
            sense.IsActive = active;
            sense.UpdatedAt = time.GetUtcNow();
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Corrects a word's text. A real change raises the content version, so old AI and audio are not reused (spec AU05).</summary>
    public async Task UpdateTextAsync(Guid ownerId, Guid senseId, string korean, string? koreanNote, string uzbek, CancellationToken ct)
    {
        var sense = await FindAsync(ownerId, senseId, ct);
        var note = string.IsNullOrWhiteSpace(koreanNote) ? null : koreanNote.Trim();
        var changed = TextNormalizer.Key(sense.Korean) != TextNormalizer.Key(korean)
            || TextNormalizer.Key(sense.Uzbek) != TextNormalizer.Key(uzbek)
            || TextNormalizer.Key(sense.KoreanNote) != TextNormalizer.Key(note);
        if (!changed)
        {
            return;
        }

        sense.Korean = korean.Trim();
        sense.KoreanNote = note;
        sense.Uzbek = uzbek.Trim();
        sense.ContentVersion++;
        sense.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    private async Task<WordSense> FindAsync(Guid ownerId, Guid senseId, CancellationToken ct) =>
        await db.WordSenses.SingleOrDefaultAsync(s => s.Id == senseId && s.OwnerId == ownerId, ct)
            ?? throw new NotFoundException("So‘z topilmadi.");
}
