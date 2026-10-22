using Lugat.Application.Abstractions;
using Lugat.Contracts;
using Lugat.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Lugat.Application.Catalog;

/// <summary>
/// The catalog as learners see it (spec §11): published units only. A unit still being prepared is only counted
/// ("Tez orada"); its number, words and id never leave the server (spec §13.3).
/// Which units a learner has opened comes with unit progress in stage 3.
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

    public async Task<LevelUnitsDto> UnitsAsync(CatalogLevel level, CancellationToken ct)
    {
        var units = await db.Units.Where(u => u.Level == level && u.Status == UnitStatus.Published).OrderBy(u => u.Number)
            .Select(u => new UnitSummaryDto(u.Id, u.Level, u.Number, u.WordCount)).ToListAsync(ct);
        var comingSoon = await db.Units.CountAsync(u => u.Level == level && u.Status != UnitStatus.Published, ct);
        return new LevelUnitsDto(level, Title(level), units, comingSoon);
    }

    public async Task<UnitDto> UnitAsync(Guid unitId, CancellationToken ct)
    {
        var unit = await db.Units.SingleOrDefaultAsync(u => u.Id == unitId && u.Status == UnitStatus.Published, ct)
            ?? throw new NotFoundException("Bo‘lim topilmadi.");
        var words = await db.CatalogWords.Where(w => w.UnitId == unitId && w.TranslationUz != null).OrderBy(w => w.OrderInUnit).ToListAsync(ct);
        return new UnitDto(unit.Id, unit.Level, unit.Number, words.Select(w => new CatalogWordDto(
            w.Id, w.OrderInUnit ?? 0, w.Word, w.Homonym, w.Pos, CatalogPos.Uzbek(w.Pos), w.TranslationUz!, w.Romanization,
            w.ExampleKo, w.ExampleUz, w.TrapUz, w.Theme, w.Source)).ToList());
    }
}
