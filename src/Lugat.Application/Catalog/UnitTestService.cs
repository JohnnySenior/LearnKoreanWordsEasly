using Lugat.Application.Abstractions;
using Lugat.Application.Speech;
using Lugat.Application.Testing;
using Lugat.Contracts;
using Lugat.Domain.Catalog;
using Lugat.Domain.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Lugat.Application.Catalog;

public sealed class UnitOptions
{
    public const string SectionName = "Units";

    /// <summary>Right answers needed, in percent (spec §4.4: 80, so 20 of 25).</summary>
    public int PassPercent { get; set; } = 80;

    /// <summary>"Bilaman" tests per unit per day (spec §4.5).</summary>
    public int SkipAttemptsPerDay { get; set; } = 3;
}

/// <summary>
/// Unit tests on the practice-test engine (spec §4.4, §4.5, D-30): the final test of an open unit and the "Bilaman"
/// test of the next locked one. Questions come without answers; the server grades, and on finishing it decides
/// whether the unit is passed (<see cref="ApplyAsync"/>). Only these results open units; nothing else may.
/// </summary>
public sealed class UnitTestService(
    IAppDbContext db,
    TimeProvider time,
    IOperationLock operations,
    UnitProgressService units,
    ISpeechSynthesizer speech,
    IOptionsMonitor<UnitOptions> options)
{
    public static int PassMark(int questions, int percent) => (int)Math.Ceiling(questions * percent / 100.0);

    public async Task<TestDto> StartAsync(Guid userId, Guid unitId, StartUnitTestRequest request, CancellationToken ct)
    {
        await using var lease = await operations.AcquireAsync($"test:{request.Id}", ct);
        if (await db.TestRuns.SingleOrDefaultAsync(r => r.Id == request.Id, ct) is { } existing)
        {
            return existing.OwnerId == userId && existing.UnitId == unitId ? TestService.ToDto(existing) : throw new NotFoundException("Sinov topilmadi.");
        }

        var purpose = request.Kind == UnitTestKind.Skip ? TestPurpose.UnitSkip : TestPurpose.UnitFinal;
        var unit = await db.Units.SingleOrDefaultAsync(u => u.Id == unitId && u.Status == UnitStatus.Published, ct)
            ?? throw new NotFoundException("Bo‘lim topilmadi.");
        if (purpose == TestPurpose.UnitFinal)
        {
            await units.AccessAsync(userId, unitId, ct); // 403 while locked
        }
        else
        {
            await CheckSkipAsync(userId, unit, ct);
        }

        var now = time.GetUtcNow();
        var words = await WordsAsync(unit, ct);
        var questions = UnitTestGenerator.Build(words.Unit, words.Pool, speech.IsConfigured, new Random(Random.Shared.Next()));
        var run = new TestRun
        {
            Id = request.Id,
            OwnerId = userId,
            Title = $"{Study.LearnerItems.Label(unit.Level, unit.Number)}-bo‘lim · {(purpose == TestPurpose.UnitSkip ? "Bilaman testi" : "yakuniy test")}",
            Status = TestRunStatus.InProgress,
            Questions = questions,
            Purpose = purpose,
            UnitId = unitId,
            PassMark = PassMark(questions.Count, options.CurrentValue.PassPercent),
            // spec §4.4: without speech, listening questions are replaced by meaning ones and the result says so
            Warning = speech.IsConfigured ? null : "Ovoz xizmati ulanmagan: tinglash savollari ma’no savollariga almashtirildi.",
            CreatedAt = now,
        };
        db.TestRuns.Add(run);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.DiscardChanges();
            return TestService.ToDto(await db.TestRuns.SingleAsync(r => r.Id == request.Id && r.OwnerId == userId, ct));
        }

        return TestService.ToDto(run);
    }

    /// <summary>
    /// Applies a finished unit test: passed when right answers ≥ the pass mark (spec §4.4). Runs once per test,
    /// inside the caller's transaction that also marks it finished.
    /// </summary>
    public async Task ApplyAsync(TestRun run, CancellationToken ct)
    {
        if (run.Purpose == TestPurpose.Practice || run.UnitId is not { } unitId || run.Passed is not null)
        {
            return;
        }

        var right = run.Answers.Where(a => a.IsCorrect).Select(a => a.Index).ToHashSet();
        var passed = right.Count >= run.PassMark;
        var percent = run.Questions.Count == 0 ? 0 : (int)Math.Round(right.Count * 100.0 / run.Questions.Count);
        var known = run.Questions.Where(q => right.Contains(q.Index)).Select(q => q.LearningItemId).OfType<Guid>().ToHashSet();
        var opened = await units.RecordTestAsync(run.OwnerId, unitId,
            run.Purpose == TestPurpose.UnitSkip ? UnitPassedBy.SkipTest : UnitPassedBy.Test, passed, percent, known, ct);
        (run.Passed, run.OpenedUnitId) = (passed, opened);
    }

    private async Task CheckSkipAsync(Guid userId, Unit unit, CancellationToken ct)
    {
        if (!await units.CanSkipAsync(userId, unit.Id, ct))
        {
            throw new ForbiddenException(unit.Number > 1
                ? $"«Bilaman» testi faqat navbatdagi bo‘lim uchun. Avval {Study.LearnerItems.Label(unit.Level, unit.Number - 1)}-bo‘limni oching yoki tugating."
                : "«Bilaman» testi faqat navbatdagi bo‘lim uchun.");
        }

        var since = await DayStartAsync(userId, ct);
        var today = await db.TestRuns.CountAsync(r => r.OwnerId == userId && r.UnitId == unit.Id && r.Purpose == TestPurpose.UnitSkip && r.CreatedAt >= since, ct);
        if (today >= options.CurrentValue.SkipAttemptsPerDay)
        {
            throw new TooManyRequestsException($"Bu bo‘lim uchun «Bilaman» testi bugun {options.CurrentValue.SkipAttemptsPerDay} marta topshirildi. Ertaga qayta urinib ko‘ring yoki oldingi bo‘limni tugating.");
        }
    }

    /// <summary>Midnight of the learner's day (their time zone), as UTC.</summary>
    private async Task<DateTimeOffset> DayStartAsync(Guid userId, CancellationToken ct)
    {
        var zoneId = await db.Users.Where(u => u.Id == userId).Select(u => u.TimeZone).SingleAsync(ct);
        var zone = TimeZoneInfo.TryFindSystemTimeZoneById(zoneId, out var found) ? found : TimeZoneInfo.Utc;
        var local = TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone);
        var midnight = new DateTimeOffset(local.Date, local.Offset);
        return midnight.ToUniversalTime();
    }

    /// <summary>The unit's words and the pool for wrong options: this unit and earlier published units (spec §4.4).</summary>
    private async Task<(List<UnitTestWord> Unit, List<UnitTestWord> Pool)> WordsAsync(Unit unit, CancellationToken ct)
    {
        var lower = Enum.GetValues<CatalogLevel>().Where(l => l < unit.Level).ToList();
        var rows = await (from i in db.LearningItems
                          join w in db.CatalogWords on i.CatalogWordId equals w.Id
                          join u in db.Units on i.UnitId equals u.Id
                          where i.IsActive && u.Status == UnitStatus.Published
                              && (lower.Contains(u.Level) || u.Level == unit.Level && u.Number <= unit.Number)
                          select new { i.Id, i.Korean, i.KoreanNote, i.Uzbek, w.Pos, i.UnitId }).ToListAsync(ct);
        var all = rows.Select(r => (r.UnitId, Word: new UnitTestWord(r.Id, r.Korean, r.KoreanNote is null ? r.Korean : $"{r.Korean} {r.KoreanNote}", r.Uzbek, r.Pos))).ToList();
        var own = all.Where(x => x.UnitId == unit.Id).Select(x => x.Word).ToList();
        return own.Count < 4
            ? throw new ValidationException("Bu bo‘limda test uchun so‘z yetarli emas.")
            : (own, all.Select(x => x.Word).ToList());
    }
}
