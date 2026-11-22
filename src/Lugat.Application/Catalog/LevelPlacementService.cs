using Lugat.Application.Abstractions;
using Lugat.Application.Speech;
using Lugat.Application.Testing;
using Lugat.Contracts;
using Lugat.Domain.Catalog;
using Lugat.Domain.Testing;
using Microsoft.EntityFrameworkCore;

namespace Lugat.Application.Catalog;

/// <summary>
/// L-90: the exam that opens a level (B, C) for a learner who already knows the one below. 40 choice questions on
/// words of the level below, one or two from each of its units, easy to hard (unit order): Korean → Uzbek meaning,
/// Uzbek → Korean word and listening, as in the unit test. 85% passes: every unit of the level below counts as
/// passed (without cards, so reviews are not flooded) and the level's first unit opens; words answered wrong become
/// new cards. A fail says how many first units the learner knows; the learner may open those and start after them.
/// Twice a day per level.
/// </summary>
public sealed class LevelPlacementService(IAppDbContext db, TimeProvider time, IOperationLock operations, UnitProgressService units, ISpeechSynthesizer speech)
{
    public const int Questions = 40;
    public const int PassPercent = 85;
    public const int AttemptsPerDay = 2;

    public async Task<TestDto> StartAsync(Guid userId, CatalogLevel level, Guid id, CancellationToken ct)
    {
        await using var dayLease = await operations.AcquireAsync($"placement:{userId}:{level}", ct);
        await using var lease = await operations.AcquireAsync($"test:{id}", ct);
        if (await db.TestRuns.SingleOrDefaultAsync(r => r.Id == id, ct) is { } existing)
        {
            return existing.OwnerId == userId && existing.Purpose == TestPurpose.LevelPlacement ? TestService.ToDto(existing) : throw new NotFoundException("Sinov topilmadi.");
        }

        var offer = (await units.UnitsAsync(userId, level, ct)).Placement
            ?? throw new ForbiddenException($"{level} daraja uchun imtihon hozir yo‘q: bu daraja allaqachon ochiq yoki hali tayyor emas.");
        var target = await db.Units.Where(u => u.Level == level && u.Number == 1 && u.Status == UnitStatus.Published).Select(u => u.Id).SingleAsync(ct);
        var since = await DayStartAsync(userId, ct);
        if (await db.TestRuns.CountAsync(r => r.OwnerId == userId && r.UnitId == target && r.Purpose == TestPurpose.LevelPlacement && r.CreatedAt >= since, ct) >= AttemptsPerDay)
        {
            throw new TooManyRequestsException($"{level} darajani ochish imtihoni bugun {AttemptsPerDay} marta topshirildi. Ertaga qayta urinib ko‘ring.");
        }

        var words = await WordsAsync(offer.From, ct);
        var questions = Build(words, speech.IsConfigured, new Random(Random.Shared.Next()));
        if (questions.Count < 10)
        {
            throw new ValidationException("Imtihon uchun so‘z yetarli emas.");
        }

        var run = new TestRun
        {
            Id = id,
            OwnerId = userId,
            Title = $"{level} darajani ochish imtihoni",
            Status = TestRunStatus.InProgress,
            Questions = questions,
            Purpose = TestPurpose.LevelPlacement,
            UnitId = target,
            Level = level,
            PassMark = UnitTestService.PassMark(questions.Count, PassPercent),
            Warning = speech.IsConfigured ? null : "Ovoz xizmati ulanmagan: tinglash savollari ma’no savollariga almashtirildi.",
            CreatedAt = time.GetUtcNow(),
        };
        db.TestRuns.Add(run);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.DiscardChanges();
            return TestService.ToDto(await db.TestRuns.SingleAsync(r => r.Id == id && r.OwnerId == userId, ct));
        }

        return TestService.ToDto(run);
    }

    /// <summary>
    /// The questions: <see cref="Questions"/> words spread over the units in order (one or two from each; every other
    /// unit when there are more units than questions), so they go from easy to hard. A quarter is heard.
    /// </summary>
    public static List<TestQuestion> Build(IReadOnlyList<(int Unit, UnitTestWord Word)> words, bool listening, Random random)
    {
        var byUnit = words.GroupBy(w => w.Unit).OrderBy(g => g.Key).Select(g => g.Select(x => x.Word).OrderBy(_ => random.Next()).ToList()).ToList();
        if (byUnit.Count == 0)
        {
            return [];
        }

        var chosen = new List<UnitTestWord>();
        var taken = new int[byUnit.Count];
        var count = Math.Min(Questions, words.Count);
        for (var i = 0; i < count; i++)
        {
            var unit = (int)((long)i * byUnit.Count / count);
            for (var step = 0; step < byUnit.Count; step++)
            {
                var u = (unit + step) % byUnit.Count;
                if (taken[u] < byUnit[u].Count)
                {
                    chosen.Add(byUnit[u][taken[u]++]);
                    break;
                }
            }
        }

        var heard = listening ? (int)Math.Round(chosen.Count / 4.0, MidpointRounding.AwayFromZero) : 0;
        var meaning = (chosen.Count - heard + 1) / 2;
        var kinds = Enumerable.Repeat(TestQuestionKind.WordMeaning, meaning)
            .Concat(Enumerable.Repeat(TestQuestionKind.WordKorean, chosen.Count - heard - meaning))
            .Concat(Enumerable.Repeat(TestQuestionKind.WordListening, heard))
            .OrderBy(_ => random.Next()).ToList();
        var pool = words.Select(w => w.Word).ToList();
        var twins = pool.Select(p => new Vocabulary.MeaningTwins.Twin(p.ItemId, p.Korean, p.Uzbek, p.Pos)).ToList();
        return chosen.Select((w, i) => UnitTestGenerator.Question(kinds[i], w, pool, twins, random) with { Index = i }).ToList();
    }

    /// <summary>
    /// The first units the learner knows: the longest run of units from the start whose questions are at least
    /// <see cref="PassPercent"/> right (and at least four questions, so one lucky answer does not count).
    /// </summary>
    public static int KnownUnits(IReadOnlyList<(int Unit, bool Right)> answers)
    {
        var known = 0;
        var (asked, right) = (0, 0);
        foreach (var unit in answers.GroupBy(a => a.Unit).OrderBy(g => g.Key))
        {
            asked += unit.Count();
            right += unit.Count(a => a.Right);
            if (asked >= 4 && right * 100 >= asked * PassPercent)
            {
                known = unit.Key;
            }
        }

        return known;
    }

    /// <summary>Applies a finished exam once, inside the transaction that finishes it.</summary>
    public async Task ApplyAsync(TestRun run, CancellationToken ct)
    {
        var right = run.Answers.Where(a => a.IsCorrect).Select(a => a.Index).ToHashSet();
        var (below, numbers) = await UnitsOfAsync(run, ct);
        var answers = run.Questions.Where(q => q.LearningItemId is { } item && numbers.ContainsKey(item))
            .Select(q => (numbers[q.LearningItemId!.Value], right.Contains(q.Index))).ToList();
        var passed = right.Count >= run.PassMark;
        var all = await db.Units.Where(u => u.Level == below && u.Status == UnitStatus.Published).OrderBy(u => u.Number).Select(u => u.Id).ToListAsync(ct);
        run.Passed = passed;
        run.KnownUnits = passed ? all.Count : KnownUnits(answers);
        if (passed)
        {
            run.OpenedUnitId = await units.PlaceAsync(run.OwnerId, all, run.UnitId, Wrong(run, right), ct);
        }
    }

    /// <summary>After a fail: opens the first units the learner knows, so they start from the next one. Once.</summary>
    public async Task<TestDto> OpenKnownAsync(Guid userId, Guid testId, CancellationToken ct)
    {
        await using var lease = await operations.AcquireAsync($"test:{testId}", ct);
        var run = await db.TestRuns.SingleOrDefaultAsync(r => r.Id == testId && r.OwnerId == userId && r.Purpose == TestPurpose.LevelPlacement, ct)
            ?? throw new NotFoundException("Sinov topilmadi.");
        if (run.Status != TestRunStatus.Finished || run.Passed != false || run.KnownUnits is not > 0)
        {
            throw new ValidationException("Ochiladigan bo‘lim yo‘q.");
        }

        if (run.OpenedUnitId is null)
        {
            var (below, numbers) = await UnitsOfAsync(run, ct);
            var known = await db.Units.Where(u => u.Level == below && u.Status == UnitStatus.Published && u.Number <= run.KnownUnits).Select(u => u.Id).ToListAsync(ct);
            var right = run.Answers.Where(a => a.IsCorrect).Select(a => a.Index).ToHashSet();
            var wrong = Wrong(run, right).Where(item => numbers.GetValueOrDefault(item) is var n && n > 0 && n <= run.KnownUnits).ToList();
            run.OpenedUnitId = await units.PlaceAsync(userId, known, null, wrong, ct);
            await db.SaveChangesAsync(ct);
        }

        return TestService.ToDto(run);
    }

    private static List<Guid> Wrong(TestRun run, HashSet<int> right) =>
        run.Questions.Where(q => !right.Contains(q.Index)).Select(q => q.LearningItemId).OfType<Guid>().Distinct().ToList();

    /// <summary>The level below the exam's level, and the unit number of each word asked.</summary>
    private async Task<(CatalogLevel Below, Dictionary<Guid, int> Numbers)> UnitsOfAsync(TestRun run, CancellationToken ct)
    {
        var level = await db.Units.Where(u => u.Id == run.UnitId).Select(u => u.Level).SingleAsync(ct);
        var items = run.Questions.Select(q => q.LearningItemId).OfType<Guid>().ToList();
        var numbers = await (from i in db.LearningItems
                             join u in db.Units on i.UnitId equals u.Id
                             where items.Contains(i.Id)
                             select new { i.Id, u.Number }).ToDictionaryAsync(x => x.Id, x => x.Number, ct);
        return (level - 1, numbers);
    }

    /// <summary>The words of the level's published units with their unit number, and the same pool for wrong options.</summary>
    private async Task<List<(int Unit, UnitTestWord Word)>> WordsAsync(CatalogLevel below, CancellationToken ct)
    {
        var rows = await (from i in db.LearningItems
                          join w in db.CatalogWords on i.CatalogWordId equals w.Id
                          join u in db.Units on i.UnitId equals u.Id
                          where i.IsActive && u.Status == UnitStatus.Published && u.Level == below
                          select new { i.Id, i.Korean, i.KoreanNote, i.Uzbek, w.Pos, u.Number }).ToListAsync(ct);
        return rows.Select(r => (r.Number, new UnitTestWord(r.Id, r.Korean, r.KoreanNote is null ? r.Korean : $"{r.Korean} {r.KoreanNote}", r.Uzbek, r.Pos))).ToList();
    }

    /// <summary>Midnight of the learner's day (their time zone), as UTC.</summary>
    private async Task<DateTimeOffset> DayStartAsync(Guid userId, CancellationToken ct)
    {
        var zoneId = await db.Users.Where(u => u.Id == userId).Select(u => u.TimeZone).SingleAsync(ct);
        var zone = TimeZoneInfo.TryFindSystemTimeZoneById(zoneId, out var found) ? found : TimeZoneInfo.Utc;
        var local = TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone);
        return new DateTimeOffset(local.Date, local.Offset).ToUniversalTime();
    }
}
