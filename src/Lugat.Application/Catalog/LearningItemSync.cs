using Lugat.Application.Abstractions;
using Lugat.Domain.Catalog;
using Lugat.Domain.Vocabulary;
using Microsoft.EntityFrameworkCore;

namespace Lugat.Application.Catalog;

/// <summary>
/// Keeps the shared learning items of a published unit in step with its catalog words (spec §5.2): created when the
/// unit is published, updated when a word is edited afterwards. The text change raises the item's content version
/// like the word's (spec §5.3: cards and statistics stay, audio and AI artefacts go stale).
/// </summary>
public static class LearningItemSync
{
    /// <summary>Upserts the unit's items; the caller saves.</summary>
    public static async Task SyncUnitAsync(IAppDbContext db, Unit unit, DateTimeOffset now, CancellationToken ct)
    {
        if (unit.Status != UnitStatus.Published)
        {
            return;
        }

        var words = await db.CatalogWords.Where(w => w.UnitId == unit.Id && w.TranslationUz != null).ToListAsync(ct);
        var ids = words.Select(w => (Guid?)w.Id).ToList();
        var items = await db.LearningItems.Where(i => ids.Contains(i.CatalogWordId)).ToDictionaryAsync(i => i.CatalogWordId!.Value, ct);
        // Two words written the same in one unit are told apart on the card (spec §2.2).
        var twins = words.GroupBy(w => w.Word).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        foreach (var word in words)
        {
            if (!items.TryGetValue(word.Id, out var item))
            {
                item = new LearningItem
                {
                    Id = Guid.CreateVersion7(now),
                    Kind = LearningItemKind.Catalog,
                    CatalogWordId = word.Id,
                    CreatedAt = now,
                };
                db.LearningItems.Add(item);
            }

            var note = twins.Contains(word.Word) ? word.Gloss ?? CatalogPos.Uzbek(word.Pos) : null;
            var changed = item.Korean != word.Word || item.KoreanNote != note || item.Uzbek != word.TranslationUz;
            (item.UnitId, item.Order) = (unit.Id, LearningItem.CourseOrder(unit.Level, unit.Number, word.OrderInUnit ?? 0));
            if (changed)
            {
                if (item.Korean.Length > 0)
                {
                    item.ContentVersion++;
                }

                (item.Korean, item.KoreanNote, item.Uzbek, item.UpdatedAt) = (word.Word, note, word.TranslationUz!, now);
            }
        }
    }

    /// <summary>
    /// Fills in the items of published units that have fewer items than words, e.g. units published before learning
    /// items existed (L-22). Runs at startup; a unit that is complete costs one count.
    /// </summary>
    public static async Task<int> SyncMissingAsync(IAppDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        var incomplete = await db.Units
            .Where(u => u.Status == UnitStatus.Published
                && db.CatalogWords.Count(w => w.UnitId == u.Id && w.TranslationUz != null) != db.LearningItems.Count(i => i.UnitId == u.Id))
            .ToListAsync(ct);
        foreach (var unit in incomplete)
        {
            await SyncUnitAsync(db, unit, now, ct);
        }

        await db.SaveChangesAsync(ct);
        return incomplete.Count;
    }
}
