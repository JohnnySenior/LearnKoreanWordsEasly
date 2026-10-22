using Lugat.Application.Abstractions;
using Lugat.Application.Vocabulary;
using Lugat.Contracts;
using Lugat.Domain.Catalog;
using Lugat.Domain.Vocabulary;
using Microsoft.EntityFrameworkCore;

namespace Lugat.Application.Catalog;

/// <summary>
/// Content work on the shared catalog (spec §2.4): import of the official list, splitting into units, editing and
/// checking words, publishing units. Changes that touch many words run one at a time under the "catalog" lock.
/// </summary>
public sealed class CatalogAdminService(IAppDbContext db, TimeProvider time, IOperationLock operations, ICatalogFileReader reader)
{
    public const string LockKey = "catalog";

    public const int MaxQuestions = 3;

    public async Task<IReadOnlyList<CatalogLevelAdminDto>> LevelsAsync(CancellationToken ct)
    {
        var words = await db.CatalogWords.GroupBy(w => w.Level).Select(g => new
        {
            Level = g.Key,
            Words = g.Count(),
            Studyable = g.Count(w => w.IsStudyable),
            Unplaced = g.Count(w => w.IsStudyable && w.UnitId == null),
            Reviewed = g.Count(w => w.Status == CatalogWordStatus.Reviewed),
        }).ToListAsync(ct);
        var units = await db.Units.GroupBy(u => u.Level).Select(g => new
        {
            Level = g.Key,
            Units = g.Count(),
            Published = g.Count(u => u.Status == UnitStatus.Published),
        }).ToListAsync(ct);
        return Enum.GetValues<CatalogLevel>().Select(level =>
        {
            var w = words.FirstOrDefault(x => x.Level == level);
            var u = units.FirstOrDefault(x => x.Level == level);
            return new CatalogLevelAdminDto(level, w?.Words ?? 0, w?.Studyable ?? 0, w?.Unplaced ?? 0, u?.Units ?? 0, u?.Published ?? 0, w?.Reviewed ?? 0);
        }).ToList();
    }

    /// <summary>
    /// Saves the official fields of every row (spec §2.4 step 1). A word already in the catalog (same level, word,
    /// homonym, part of speech) only gets its rank and gloss refreshed, so importing again never duplicates and
    /// never touches content, review state or units.
    /// </summary>
    public async Task<CatalogImportResultDto> ImportAsync(Stream content, string fileName, CancellationToken ct)
    {
        var parsed = CatalogImport.Parse(reader.ReadRows(content, fileName));
        if (parsed.Rows.Count == 0)
        {
            throw new ValidationException("Faylda o‘qiladigan so‘z qatori yo‘q." + (parsed.Problems.Count > 0 ? " " + parsed.Problems[0] : ""));
        }

        await using var lease = await operations.AcquireAsync(LockKey, ct);
        var existing = await db.CatalogWords.ToDictionaryAsync(w => w.SourceKey, ct);
        var now = time.GetUtcNow();
        int created = 0, updated = 0, unchanged = 0;
        foreach (var row in parsed.Rows)
        {
            if (existing.TryGetValue(row.Key, out var word))
            {
                if (word.FreqRank == row.Rank && word.Gloss == row.Gloss)
                {
                    unchanged++;
                    continue;
                }

                // A new rank does not move a word that is already in a unit (spec §2.2: units are stable).
                (word.FreqRank, word.Gloss, word.UpdatedAt) = (row.Rank, row.Gloss, now);
                updated++;
                continue;
            }

            db.CatalogWords.Add(NewWord(row, now));
            created++;
        }

        await db.SaveChangesAsync(ct);
        return new CatalogImportResultDto(parsed.DataRows, created, updated, unchanged, parsed.DataRows - parsed.Rows.Count, parsed.Problems, await LevelsAsync(ct));
    }

    public static CatalogWord NewWord(CatalogRow row, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(now),
        SourceKey = row.Key,
        Word = row.Word,
        Homonym = row.Homonym,
        Pos = row.Pos,
        Gloss = row.Gloss,
        Level = row.Level,
        FreqRank = row.Rank,
        IsStudyable = CatalogWord.StudyablePos(row.Pos),
        Status = CatalogWordStatus.Draft,
        CreatedAt = now,
        UpdatedAt = now,
    };

    /// <summary>
    /// Splits the level into units of 25 by frequency (spec §2.2 step 2). Published units and their words never
    /// change; every other studyable word is placed again after them, so the split can be repeated after an import.
    /// </summary>
    public async Task<CatalogLevelAdminDto> SplitAsync(CatalogLevel level, CancellationToken ct)
    {
        await using var lease = await operations.AcquireAsync(LockKey, ct);
        await SplitUnlockedAsync(level, ct);
        return (await LevelsAsync(ct)).Single(l => l.Level == level);
    }

    /// <summary>The split itself; the caller holds the catalog lock.</summary>
    public async Task SplitUnlockedAsync(CatalogLevel level, CancellationToken ct)
    {
        var units = await db.Units.Where(u => u.Level == level).ToListAsync(ct);
        var published = units.Where(u => u.Status == UnitStatus.Published).Select(u => u.Id).ToHashSet();
        var words = await db.CatalogWords.Where(w => w.Level == level).ToListAsync(ct);
        foreach (var word in words.Where(w => w.UnitId is { } id && !published.Contains(id)))
        {
            (word.UnitId, word.OrderInUnit) = (null, null);
        }

        var place = words.Where(w => w.IsStudyable && w.UnitId is null).OrderBy(w => w.FreqRank).ThenBy(w => w.SourceKey, StringComparer.Ordinal).ToList();
        var first = units.Where(u => published.Contains(u.Id)).Select(u => u.Number).DefaultIfEmpty(0).Max() + 1;
        var plan = UnitSplitter.Plan(place, first);
        var drafts = units.Where(u => !published.Contains(u.Id)).ToDictionary(u => u.Number);
        var now = time.GetUtcNow();
        foreach (var (number, unitWords) in plan.Units)
        {
            if (!drafts.Remove(number, out var unit))
            {
                unit = new Unit { Id = Guid.CreateVersion7(now), Level = level, Number = number, Status = UnitStatus.Draft, CreatedAt = now };
                db.Units.Add(unit);
            }

            unit.WordCount = unitWords.Count;
            for (var i = 0; i < unitWords.Count; i++)
            {
                (unitWords[i].UnitId, unitWords[i].OrderInUnit) = (unit.Id, i + 1);
            }
        }

        // Words leave the units first, then the empty units go (two saves keep the foreign key satisfied).
        await db.SaveChangesAsync(ct);
        db.Units.RemoveRange(drafts.Values);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<UnitAdminSummaryDto>> UnitsAsync(CatalogLevel level, CancellationToken ct)
    {
        var units = await db.Units.Where(u => u.Level == level).OrderBy(u => u.Number).ToListAsync(ct);
        var counts = await db.CatalogWords.Where(w => w.Level == level && w.UnitId != null).GroupBy(w => w.UnitId!.Value).Select(g => new
        {
            Id = g.Key,
            Drafted = g.Count(w => w.TranslationUz != null),
            Reviewed = g.Count(w => w.Status == CatalogWordStatus.Reviewed),
        }).ToDictionaryAsync(x => x.Id, ct);
        return units.Select(u => Summary(u, counts.GetValueOrDefault(u.Id)?.Drafted ?? 0, counts.GetValueOrDefault(u.Id)?.Reviewed ?? 0)).ToList();
    }

    public async Task<UnitAdminDto> UnitAsync(Guid unitId, CancellationToken ct)
    {
        var unit = await db.Units.SingleOrDefaultAsync(u => u.Id == unitId, ct) ?? throw new NotFoundException("Bo‘lim topilmadi.");
        var words = await db.CatalogWords.Where(w => w.UnitId == unitId).OrderBy(w => w.OrderInUnit).ToListAsync(ct);
        var dtos = await ToDtosAsync(words, ct);
        return new UnitAdminDto(
            Summary(unit, words.Count(w => w.TranslationUz != null), words.Count(w => w.Status == CatalogWordStatus.Reviewed)),
            dtos,
            words.Count(CatalogDraftService.NeedsDraft));
    }

    /// <summary>
    /// Saves an editor's changes (spec §2.4 step 4). The official fields cannot change. Any content change raises
    /// <see cref="CatalogWord.ContentVersion"/>; a checked word stays checked, now by the editor who changed it.
    /// </summary>
    public async Task<CatalogWordAdminDto> UpdateWordAsync(Guid editorId, Guid wordId, UpdateCatalogWordRequest request, CancellationToken ct)
    {
        var (fields, problem) = CatalogContent.Check(new(request.TranslationUz, request.Romanization, request.ExampleKo, request.ExampleUz, request.TrapUz, request.Theme));
        if (fields is null)
        {
            throw new ValidationException(problem!);
        }

        var questions = new List<UsageQuestion>();
        foreach (var q in request.Questions ?? [])
        {
            questions.Add(UsageService.CheckQuestion(q.Uzbek, q.Sentence, q.Answer, q.Wrong, q.ExplanationUz)
                ?? throw new ValidationException("Savol to‘liq emas: o‘zbekcha gap, bitta «___» bo‘shliqli koreyscha gap, javob, kamida bitta noto‘g‘ri variant va izoh kerak."));
        }

        if (questions.Count > MaxQuestions)
        {
            throw new ValidationException($"Ko‘pi bilan {MaxQuestions} ta savol.");
        }

        await using var lease = await operations.AcquireAsync(LockKey, ct);
        var word = await LoadAsync(wordId, request.Version, ct);
        var publishedUnit = word.UnitId is { } unitId ? await db.Units.SingleOrDefaultAsync(u => u.Id == unitId && u.Status == UnitStatus.Published, ct) : null;
        if (publishedUnit is not null && !fields.IsComplete)
        {
            throw new ValidationException("Nashr qilingan bo‘limdagi so‘zda tarjima, o‘qilishi va misol bo‘lishi shart.");
        }

        var now = time.GetUtcNow();
        if (Fields(word) != fields)
        {
            (word.TranslationUz, word.Romanization, word.ExampleKo, word.ExampleUz, word.TrapUz, word.Theme) =
                (fields.TranslationUz, fields.Romanization, fields.ExampleKo, fields.ExampleUz, fields.TrapUz, fields.Theme);
            word.ContentVersion++;
            word.UpdatedAt = now;
            if (word.Status == CatalogWordStatus.Reviewed)
            {
                (word.ReviewedByUserId, word.ReviewedAt) = (editorId, now);
            }
        }

        var usage = await db.CatalogUsages.SingleOrDefaultAsync(u => u.CatalogWordId == word.Id, ct);
        if (usage is null && questions.Count > 0)
        {
            usage = new CatalogUsage { Id = Guid.CreateVersion7(now), CatalogWordId = word.Id, CreatedAt = now };
            db.CatalogUsages.Add(usage);
        }

        if (usage is not null)
        {
            (usage.Questions, usage.ContentVersion, usage.UpdatedAt) = (questions, word.ContentVersion, now);
        }

        if (publishedUnit is not null)
        {
            // Learners already practise this word: their cards follow the corrected text (spec §5.3).
            await SaveAsync(ct);
            await LearningItemSync.SyncUnitAsync(db, publishedUnit, now, ct);
        }

        await SaveAsync(ct);
        return (await ToDtosAsync([word], ct))[0];
    }

    /// <summary>"Tekshirildi": a person confirms the word (spec §2.4 step 4); the reviewer is kept.</summary>
    public async Task<CatalogWordAdminDto> ReviewAsync(Guid editorId, Guid wordId, uint version, CancellationToken ct)
    {
        await using var lease = await operations.AcquireAsync(LockKey, ct);
        var word = await LoadAsync(wordId, version, ct);
        if (!Fields(word).IsComplete)
        {
            throw new ValidationException("Avval tarjima, lotincha o‘qilishi va misolni to‘ldiring.");
        }

        var now = time.GetUtcNow();
        (word.Status, word.ReviewedByUserId, word.ReviewedAt, word.UpdatedAt) = (CatalogWordStatus.Reviewed, editorId, now, now);
        await SaveAsync(ct);
        return (await ToDtosAsync([word], ct))[0];
    }

    /// <summary>
    /// Shows the unit to learners (spec §2.4 step 5): only when every word is checked, and in order, so a level has
    /// no gaps. Publishing again is a no-op.
    /// </summary>
    public async Task<UnitAdminSummaryDto> PublishAsync(Guid unitId, CancellationToken ct)
    {
        await using var lease = await operations.AcquireAsync(LockKey, ct);
        var unit = await db.Units.SingleOrDefaultAsync(u => u.Id == unitId, ct) ?? throw new NotFoundException("Bo‘lim topilmadi.");
        var words = await db.CatalogWords.Where(w => w.UnitId == unitId).Select(w => new { w.Status, Draft = w.TranslationUz != null }).ToListAsync(ct);
        if (unit.Status != UnitStatus.Published)
        {
            var waiting = words.Count(w => w.Status != CatalogWordStatus.Reviewed);
            if (words.Count == 0 || waiting > 0)
            {
                throw new ValidationException($"Bo‘limda {waiting} ta so‘z hali tekshirilmagan. Hammasi «Tekshirildi» bo‘lgach nashr qiling.");
            }

            if (await db.Units.Where(u => u.Level == unit.Level && u.Number < unit.Number && u.Status != UnitStatus.Published)
                    .OrderBy(u => u.Number).Select(u => (int?)u.Number).FirstOrDefaultAsync(ct) is { } earlier)
            {
                throw new ValidationException($"Avval {unit.Level} darajaning {earlier}-bo‘limini nashr qiling.");
            }

            (unit.Status, unit.PublishedAt) = (UnitStatus.Published, time.GetUtcNow());
            await LearningItemSync.SyncUnitAsync(db, unit, unit.PublishedAt.Value, ct);
            await SaveAsync(ct);
        }

        return Summary(unit, words.Count(w => w.Draft), words.Count(w => w.Status == CatalogWordStatus.Reviewed));
    }

    private async Task<CatalogWord> LoadAsync(Guid wordId, uint version, CancellationToken ct)
    {
        var word = await db.CatalogWords.SingleOrDefaultAsync(w => w.Id == wordId, ct) ?? throw new NotFoundException("So‘z topilmadi.");
        return word.Version == version ? word : throw new ConflictException("So‘z boshqa joyda o‘zgartirilgan. Sahifani yangilab, qaytadan tahrirlang.");
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.DiscardChanges();
            throw new ConflictException("So‘z boshqa joyda o‘zgartirilgan. Sahifani yangilab, qaytadan tahrirlang.");
        }
    }

    public static CatalogContentFields Fields(CatalogWord w) => new(w.TranslationUz, w.Romanization, w.ExampleKo, w.ExampleUz, w.TrapUz, w.Theme);

    private static UnitAdminSummaryDto Summary(Unit u, int drafted, int reviewed) =>
        new(u.Id, u.Level, u.Number, u.Status, u.WordCount, drafted, reviewed, u.PublishedAt);

    private async Task<List<CatalogWordAdminDto>> ToDtosAsync(IReadOnlyList<CatalogWord> words, CancellationToken ct)
    {
        var ids = words.Select(w => w.Id).ToList();
        var reviewers = words.Select(w => w.ReviewedByUserId).OfType<Guid>().Distinct().ToList();
        var names = await db.Users.Where(u => reviewers.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        var usages = await db.CatalogUsages.Where(u => ids.Contains(u.CatalogWordId)).ToDictionaryAsync(u => u.CatalogWordId, ct);
        return words.Select(w => new CatalogWordAdminDto(
            w.Id, w.OrderInUnit, w.Word, w.Homonym, w.Pos, CatalogPos.Uzbek(w.Pos), w.Gloss, w.FreqRank,
            w.TranslationUz, w.Romanization, w.ExampleKo, w.ExampleUz, w.TrapUz, w.Theme,
            w.Status, w.ContentVersion,
            w.ReviewedByUserId is { } r ? names.GetValueOrDefault(r) ?? "—" : w.Status == CatalogWordStatus.Reviewed ? "Namuna fayl" : null,
            w.ReviewedAt, w.AiProvider,
            (usages.GetValueOrDefault(w.Id)?.Questions ?? []).Select(q => new CatalogQuestionDto(q.Uzbek, q.Sentence, q.Answer, q.Wrong, q.ExplanationUz)).ToList(),
            w.Version)).ToList();
    }
}
