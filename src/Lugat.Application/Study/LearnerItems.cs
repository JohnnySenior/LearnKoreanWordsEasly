using Lugat.Application.Abstractions;
using Lugat.Domain.Catalog;
using Lugat.Domain.Vocabulary;
using Microsoft.EntityFrameworkCore;

namespace Lugat.Application.Study;

/// <summary>
/// "The learner's words" (spec §5.2): catalog items are shared, so a learner's words are the active items they have
/// cards for, i.e. the words of the units they opened.
/// </summary>
public static class LearnerItems
{
    public static IQueryable<LearningItem> Of(IAppDbContext db, Guid ownerId) =>
        db.LearningItems.Where(i => i.IsActive && db.Cards.Any(c => c.OwnerId == ownerId && c.LearningItemId == i.Id));

    /// <summary>"A-3" for unit 3 of level A.</summary>
    public static string Label(CatalogLevel level, int number) => $"{level}-{number}";

    /// <summary>Labels of the given units, for display next to a word.</summary>
    public static async Task<Dictionary<Guid, string>> UnitLabelsAsync(IAppDbContext db, IEnumerable<Guid?> unitIds, CancellationToken ct)
    {
        var ids = unitIds.OfType<Guid>().Distinct().ToList();
        return (await db.Units.Where(u => ids.Contains(u.Id)).Select(u => new { u.Id, u.Level, u.Number }).ToListAsync(ct))
            .ToDictionary(u => u.Id, u => Label(u.Level, u.Number));
    }
}
