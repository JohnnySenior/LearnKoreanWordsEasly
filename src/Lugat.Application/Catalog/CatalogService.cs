using Lugat.Application.Abstractions;
using Lugat.Contracts;
using Lugat.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Lugat.Application.Catalog;

/// <summary>
/// The levels as learners see them (spec §11): published units only. A unit still being prepared is only counted
/// ("Tez orada"); its number, words and id never leave the server (spec §13.3). Units and their words, with the
/// learner's locks, are in <see cref="UnitProgressService"/>.
/// </summary>
public sealed class CatalogService(IAppDbContext db)
{
    public static string Title(CatalogLevel level) => level switch
    {
        CatalogLevel.A => "A — boshlang‘ich",
        CatalogLevel.B => "B — o‘rta",
        _ => "C — yuqori",
    };

    public async Task<IReadOnlyList<LevelDto>> LevelsAsync(CancellationToken ct)
    {
        var counts = await db.Units.GroupBy(u => u.Level).Select(g => new
        {
            Level = g.Key,
            Published = g.Count(u => u.Status == UnitStatus.Published),
            Draft = g.Count(u => u.Status != UnitStatus.Published),
        }).ToListAsync(ct);
        return Enum.GetValues<CatalogLevel>().Select(level =>
        {
            var c = counts.FirstOrDefault(x => x.Level == level);
            return new LevelDto(level, Title(level), c?.Published ?? 0, c?.Draft ?? 0);
        }).ToList();
    }
}
