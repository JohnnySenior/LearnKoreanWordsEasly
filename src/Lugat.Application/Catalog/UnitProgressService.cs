using Lugat.Application.Abstractions;
using Lugat.Application.Study;
using Lugat.Contracts;
using Lugat.Domain.Catalog;
using Lugat.Domain.Learning;
using Microsoft.EntityFrameworkCore;

namespace Lugat.Application.Catalog;

/// <summary>
/// Which units a learner may study (spec §4.2, §5.3). Only the server opens units: A-1 for every new learner, the
/// next unit when one is passed (stage 4 test, or later when that next unit gets published). A locked unit's
/// words are never sent (403); an unpublished one does not exist for learners (404).
/// Opening a unit for the first time creates its cards, once (spec §5.3).
/// </summary>
public sealed class UnitProgressService(IAppDbContext db, TimeProvider time, IOperationLock operations)
{
    public sealed record UnitRow(Guid Id, CatalogLevel Level, int Number, UnitStatus Status, int WordCount);

    private static readonly ReviewDirection[] Directions = [ReviewDirection.KoreanToUzbek, ReviewDirection.UzbekToKorean];

    public async Task<LevelUnitsDto> UnitsAsync(Guid userId, CatalogLevel level, CancellationToken ct)
    {
        var progress = await SyncAsync(userId, ct);
        var units = await db.Units.Where(u => u.Level == level && u.Status == UnitStatus.Published).OrderBy(u => u.Number)
            .Select(u => new UnitRow(u.Id, u.Level, u.Number, u.Status, u.WordCount)).ToListAsync(ct);
        var comingSoon = await db.Units.CountAsync(u => u.Level == level && u.Status != UnitStatus.Published, ct);
        return new LevelUnitsDto(level, CatalogService.Title(level), await SummariesAsync(userId, units, progress, ct), comingSoon);
    }

    /// <summary>The words of an open (or passed) unit, for the table and the unit page (spec §4.3).</summary>
    public async Task<UnitDto> UnitAsync(Guid userId, Guid unitId, CancellationToken ct)
    {
        var (unit, progress) = await AccessAsync(userId, unitId, ct);
        var words = await (from w in db.CatalogWords
                           join i in db.LearningItems on w.Id equals i.CatalogWordId
                           where w.UnitId == unitId
                           orderby w.OrderInUnit
                           select new { Word = w, ItemId = i.Id }).ToListAsync(ct);
        var ids = words.Select(w => w.ItemId).ToList();
        var started = (await db.Cards.Where(c => c.OwnerId == userId && ids.Contains(c.LearningItemId) && c.Phase != LearningPhase.New)
            .Select(c => c.LearningItemId).Distinct().ToListAsync(ct)).ToHashSet();
        var summary = (await SummariesAsync(userId, [unit], [progress], ct))[0];
        return new UnitDto(summary, progress.StartedAt is not null, words.Select(x => new CatalogWordDto(
            x.Word.Id, x.ItemId, started.Contains(x.ItemId), x.Word.OrderInUnit ?? 0, x.Word.Word, x.Word.Homonym, x.Word.Pos,
            CatalogPos.Uzbek(x.Word.Pos), x.Word.TranslationUz!, x.Word.Romanization, x.Word.ExampleKo, x.Word.ExampleUz,
            x.Word.TrapUz, x.Word.Theme, x.Word.Source)).ToList());
    }

    /// <summary>
    /// Starts the unit: creates the learner's two cards per word that are not there yet (spec §5.3). Opening it
    /// again, from two devices at once, or after a page reload never duplicates cards.
    /// </summary>
    public async Task<UnitDto> OpenAsync(Guid userId, Guid unitId, CancellationToken ct)
    {
        await using (await operations.AcquireAsync("units:" + userId, ct))
        {
            var (_, progress) = await AccessAsync(userId, unitId, ct);
            var now = time.GetUtcNow();
            var items = await db.LearningItems.Where(i => i.UnitId == unitId && i.IsActive).Select(i => i.Id).ToListAsync(ct);
            var existing = (await db.Cards.Where(c => c.OwnerId == userId && items.Contains(c.LearningItemId))
                    .Select(c => new { c.LearningItemId, c.Direction }).ToListAsync(ct))
                .Select(c => (c.LearningItemId, c.Direction)).ToHashSet();
            foreach (var item in items)
            {
                foreach (var direction in Directions.Where(d => !existing.Contains((item, d))))
                {
                    db.Cards.Add(new Card
                    {
                        Id = Guid.CreateVersion7(now),
                        OwnerId = userId,
                        LearningItemId = item,
                        Direction = direction,
                        Phase = LearningPhase.New,
                    });
                }
            }

            progress.StartedAt ??= now;
            await db.SaveChangesAsync(ct);
        }

        return await UnitAsync(userId, unitId, ct);
    }

    /// <summary>Records a pass as the unit test would (tests and tools); see <see cref="RecordTestAsync"/>.</summary>
    public Task<Guid?> PassAsync(Guid userId, Guid unitId, UnitPassedBy by, int scorePercent, CancellationToken ct) =>
        RecordTestAsync(userId, unitId, by, passed: true, scorePercent, [], ct);

    /// <summary>
    /// Applies a finished unit test (spec §4.4, §4.5) and returns the unit it opened, if any. The final test counts an
    /// attempt and keeps the best score; a pass marks the unit passed and opens the next. A passed "Bilaman" test also
    /// opens the skipped unit and creates its cards: words answered right start as reviews due in a day (the state a
    /// new card gets when rated "Oson", without counting as an independent recall), the others as new (L-28).
    /// The caller makes sure a test is applied only once.
    /// </summary>
    public async Task<Guid?> RecordTestAsync(Guid userId, Guid unitId, UnitPassedBy by, bool passed, int scorePercent, IReadOnlyCollection<Guid> knownItems, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        await using (await operations.AcquireAsync("units:" + userId, ct))
        {
            var progress = await db.UnitProgress.SingleOrDefaultAsync(p => p.UserId == userId && p.UnitId == unitId, ct);
            if (by == UnitPassedBy.SkipTest && passed && progress is null)
            {
                progress = new UserUnitProgress { Id = Guid.CreateVersion7(now), UserId = userId, UnitId = unitId, Status = UnitProgressStatus.Open, OpenedAt = now, StartedAt = now };
                db.UnitProgress.Add(progress);
                await AddSkippedCardsAsync(userId, unitId, knownItems, now, ct);
            }

            if (progress is null)
            {
                return null; // a failed "Bilaman" test leaves the unit locked
            }

            progress.Attempts++;
            progress.BestScorePercent = Math.Max(progress.BestScorePercent ?? 0, scorePercent);
            if (passed && progress.Status != UnitProgressStatus.Passed)
            {
                (progress.Status, progress.PassedAt, progress.PassedBy) = (UnitProgressStatus.Passed, now, by);
            }

            await db.SaveChangesAsync(ct);
        }

        if (!passed)
        {
            return null;
        }

        var after = await SyncAsync(userId, ct);
        var units = await db.Units.Select(u => new UnitRow(u.Id, u.Level, u.Number, u.Status, u.WordCount)).ToListAsync(ct);
        var next = units.FirstOrDefault(u => u.Id == unitId) is { } row ? Next(units, row) : null;
        return next is not null && after.Any(p => p.UnitId == next.Id) ? next.Id : null;
    }

    private async Task AddSkippedCardsAsync(Guid userId, Guid unitId, IReadOnlyCollection<Guid> knownItems, DateTimeOffset now, CancellationToken ct)
    {
        var items = await db.LearningItems.Where(i => i.UnitId == unitId && i.IsActive).Select(i => i.Id).ToListAsync(ct);
        var existing = (await db.Cards.Where(c => c.OwnerId == userId && items.Contains(c.LearningItemId))
                .Select(c => new { c.LearningItemId, c.Direction }).ToListAsync(ct))
            .Select(c => (c.LearningItemId, c.Direction)).ToHashSet();
        var day = LadderSchedulerV1.Ladder[1];
        foreach (var item in items)
        {
            foreach (var direction in Directions.Where(d => !existing.Contains((item, d))))
            {
                var card = new Card { Id = Guid.CreateVersion7(now), OwnerId = userId, LearningItemId = item, Direction = direction, Phase = LearningPhase.New };
                if (knownItems.Contains(item))
                {
                    (card.Phase, card.Stage, card.Interval, card.DueAt) = (LearningPhase.Review, 1, day, now + day);
                }

                db.Cards.Add(card);
            }
        }
    }

    /// <summary>
    /// "Bilaman" is offered for a locked, published unit that comes right after one of the learner's open units
    /// (spec §4.5: only the next one; to skip unit 3, unit 2 first).
    /// </summary>
    public async Task<bool> CanSkipAsync(Guid userId, Guid unitId, CancellationToken ct)
    {
        var progress = await SyncAsync(userId, ct);
        return (await NextLockedAsync(progress, ct)).Contains(unitId);
    }

    private async Task<HashSet<Guid>> NextLockedAsync(IReadOnlyList<UserUnitProgress> progress, CancellationToken ct)
    {
        var units = await db.Units.Select(u => new UnitRow(u.Id, u.Level, u.Number, u.Status, u.WordCount)).ToListAsync(ct);
        var have = progress.Select(p => p.UnitId).ToHashSet();
        return progress.Where(p => p.Status == UnitProgressStatus.Open)
            .Select(p => units.FirstOrDefault(u => u.Id == p.UnitId) is { } unit ? Next(units, unit) : null)
            .OfType<UnitRow>()
            .Where(u => u.Status == UnitStatus.Published && !have.Contains(u.Id))
            .Select(u => u.Id)
            .ToHashSet();
    }

    /// <summary>Open, not yet passed units the learner started: new words come only from these (spec §4.6).</summary>
    public async Task<HashSet<Guid>> CurrentUnitIdsAsync(Guid userId, CancellationToken ct) =>
        (await db.UnitProgress.Where(p => p.UserId == userId && p.Status == UnitProgressStatus.Open)
            .Select(p => p.UnitId).ToListAsync(ct)).ToHashSet();

    /// <summary>The unit to continue on the home page: the furthest open one.</summary>
    public async Task<UnitSummaryDto?> CurrentAsync(Guid userId, CancellationToken ct)
    {
        var progress = await SyncAsync(userId, ct);
        var open = progress.Where(p => p.Status == UnitProgressStatus.Open).Select(p => p.UnitId).ToList();
        var unit = await db.Units.Where(u => open.Contains(u.Id) && u.Status == UnitStatus.Published)
            .OrderByDescending(u => u.Level).ThenByDescending(u => u.Number)
            .Select(u => new UnitRow(u.Id, u.Level, u.Number, u.Status, u.WordCount)).FirstOrDefaultAsync(ct);
        return unit is null ? null : (await SummariesAsync(userId, [unit], progress, ct))[0];
    }

    /// <summary>
    /// Brings the learner's units up to date: A-1 for a new learner, and the unit after every passed one once it is
    /// published (the last unit of a level leads to unit 1 of the next level). Safe to call on every read.
    /// </summary>
    public async Task<List<UserUnitProgress>> SyncAsync(Guid userId, CancellationToken ct)
    {
        var progress = await db.UnitProgress.Where(p => p.UserId == userId).ToListAsync(ct);
        var units = await db.Units.Select(u => new UnitRow(u.Id, u.Level, u.Number, u.Status, u.WordCount)).ToListAsync(ct);
        var have = progress.Select(p => p.UnitId).ToHashSet();
        var due = new List<UnitRow>();
        if (progress.Count == 0 && units.FirstOrDefault(u => u.Level == CatalogLevel.A && u.Number == 1) is { } first)
        {
            due.Add(first);
        }

        foreach (var passed in progress.Where(p => p.Status == UnitProgressStatus.Passed))
        {
            if (units.FirstOrDefault(u => u.Id == passed.UnitId) is { } unit && Next(units, unit) is { } next)
            {
                due.Add(next);
            }
        }

        var now = time.GetUtcNow();
        var added = due.Where(u => u.Status == UnitStatus.Published && have.Add(u.Id))
            .Select(u => new UserUnitProgress { Id = Guid.CreateVersion7(now), UserId = userId, UnitId = u.Id, Status = UnitProgressStatus.Open, OpenedAt = now })
            .ToList();
        if (added.Count == 0)
        {
            return progress;
        }

        db.UnitProgress.AddRange(added);
        try
        {
            await db.SaveChangesAsync(ct);
            return [.. progress, .. added];
        }
        catch (DbUpdateException)
        {
            // Another request opened it at the same moment (unique user + unit): use what is there.
            db.DiscardChanges();
            return await db.UnitProgress.Where(p => p.UserId == userId).ToListAsync(ct);
        }
    }

    private static UnitRow? Next(List<UnitRow> units, UnitRow unit)
    {
        if (units.FirstOrDefault(u => u.Level == unit.Level && u.Number == unit.Number + 1) is { } next)
        {
            return next;
        }

        // The last unit of a fully prepared level leads to the next level.
        var levelDone = units.Where(u => u.Level == unit.Level).All(u => u.Status == UnitStatus.Published);
        return levelDone && unit.Level < CatalogLevel.C
            ? units.FirstOrDefault(u => u.Level == unit.Level + 1 && u.Number == 1)
            : null;
    }

    /// <summary>The published unit and the learner's progress in it; 404 when not published, 403 when locked.</summary>
    public async Task<(UnitRow Unit, UserUnitProgress Progress)> AccessAsync(Guid userId, Guid unitId, CancellationToken ct)
    {
        var unit = await db.Units.Where(u => u.Id == unitId && u.Status == UnitStatus.Published)
            .Select(u => new UnitRow(u.Id, u.Level, u.Number, u.Status, u.WordCount)).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Bo‘lim topilmadi.");
        var progress = (await SyncAsync(userId, ct)).FirstOrDefault(p => p.UnitId == unitId);
        if (progress is null)
        {
            throw new ForbiddenException(unit.Number > 1
                ? $"Bu bo‘lim hali qulflangan. Avval {LearnerItems.Label(unit.Level, unit.Number - 1)}-bo‘limni tugating."
                : "Bu bo‘lim hali qulflangan. Avval oldingi darajani tugating.");
        }

        return (unit, progress);
    }

    /// <summary>Words of one unit to answer in writing for a round to count as practice (all of them when fewer).</summary>
    public const int PracticeRoundWords = 10;

    /// <summary>
    /// Marks the "Mashq" step of these units once (L-42). <paramref name="writingRound"/>: only units where the learner
    /// has now answered <see cref="PracticeRoundWords"/> of the words in writing; otherwise every unit given counts.
    /// </summary>
    public async Task MarkPracticedAsync(Guid userId, IEnumerable<Guid?> unitIds, bool writingRound, CancellationToken ct)
    {
        var ids = unitIds.OfType<Guid>().Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        if (writingRound)
        {
            var answered = await (from w in db.WritingAttempts
                                  join i in db.LearningItems on w.LearningItemId equals i.Id
                                  where w.OwnerId == userId && ids.Contains(i.UnitId!.Value)
                                  group i.Id by i.UnitId into g
                                  select new { UnitId = g.Key!.Value, Count = g.Distinct().Count() }).ToListAsync(ct);
            var sizes = await db.Units.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.WordCount, ct);
            ids = answered.Where(a => a.Count >= Math.Min(PracticeRoundWords, Math.Max(1, sizes.GetValueOrDefault(a.UnitId)))).Select(a => a.UnitId).ToList();
        }

        var now = time.GetUtcNow();
        await db.UnitProgress.Where(p => p.UserId == userId && ids.Contains(p.UnitId) && p.PracticedAt == null)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.PracticedAt, now), ct);
    }

    private async Task<List<UnitSummaryDto>> SummariesAsync(Guid userId, IReadOnlyList<UnitRow> units, IReadOnlyList<UserUnitProgress> progress, CancellationToken ct)
    {
        var ids = units.Select(u => (Guid?)u.Id).ToList();
        var started = await (from c in db.Cards
                             join i in db.LearningItems on c.LearningItemId equals i.Id
                             where c.OwnerId == userId && ids.Contains(i.UnitId) && c.Phase != LearningPhase.New
                             group i.Id by i.UnitId into g
                             select new { UnitId = g.Key, Count = g.Distinct().Count() }).ToDictionaryAsync(x => x.UnitId!.Value, x => x.Count, ct);
        var skippable = await NextLockedAsync(progress, ct);
        var themes = (await db.CatalogWords.Where(w => ids.Contains(w.UnitId) && w.Theme != null).Select(w => new { w.UnitId, w.Theme }).ToListAsync(ct))
            .GroupBy(w => w.UnitId!.Value)
            .ToDictionary(g => g.Key, g => g.GroupBy(w => w.Theme!).OrderByDescending(t => t.Count()).ThenBy(t => t.Key).Take(3).Select(t => t.Key).ToList());
        return units.Select(u =>
        {
            var p = progress.FirstOrDefault(x => x.UnitId == u.Id);
            var state = p is null ? UnitState.Locked : p.Status == UnitProgressStatus.Passed ? UnitState.Passed : UnitState.Open;
            return new UnitSummaryDto(u.Id, u.Level, u.Number, LearnerItems.Label(u.Level, u.Number), u.WordCount, state,
                started.GetValueOrDefault(u.Id), p?.BestScorePercent, themes.GetValueOrDefault(u.Id) ?? [], skippable.Contains(u.Id),
                p?.StartedAt is not null, p?.PracticedAt is not null);
        }).ToList();
    }
}
