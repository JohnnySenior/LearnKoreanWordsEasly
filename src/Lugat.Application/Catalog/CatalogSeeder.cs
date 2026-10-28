using System.Text.Json;
using System.Text.Json.Serialization;
using Lugat.Application.Abstractions;
using Lugat.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Lugat.Application.Catalog;

public sealed class CatalogOptions
{
    public const string SectionName = "Catalog";

    /// <summary>Development and tests: load the 50-word sample as units 1–2 of level A (spec §2.4 step 6).</summary>
    public bool SeedSample { get; set; }

    /// <summary>Relative to the app folder.</summary>
    public string SamplePath { get; set; } = "content/samples/lugat_A_namuna_50.json";
}

/// <summary>One word of a content file in the sample format (spec §2.3).</summary>
public sealed record CatalogSampleWord(
    string Word,
    string? Homonym,
    string Pos,
    string Level,
    [property: JsonPropertyName("freq_rank")] int FreqRank,
    [property: JsonPropertyName("translation_uz")] string? TranslationUz,
    string? Romanization,
    [property: JsonPropertyName("example_ko")] string? ExampleKo,
    [property: JsonPropertyName("example_uz")] string? ExampleUz,
    [property: JsonPropertyName("trap_uz")] string? TrapUz,
    string? Theme,
    string? Source,
    [property: JsonPropertyName("trap_cue")] string? TrapCue = null,
    [property: JsonPropertyName("trap_wrong")] string? TrapWrong = null,
    [property: JsonPropertyName("trap_right")] string? TrapRight = null);

/// <summary>
/// Loads the sample content (spec §2.4 step 6). Its content was written and checked by Johnny, so the words come in
/// checked and complete units are published. Running it again changes nothing; a word an editor already changed is
/// left alone. Production does not seed: there the admin imports the official list.
/// </summary>
public sealed class CatalogSeeder(IAppDbContext db, TimeProvider time, IOperationLock operations, CatalogAdminService admin, ILogger<CatalogSeeder> logger)
{
    public static IReadOnlyList<CatalogSampleWord> Read(Stream json) =>
        JsonSerializer.Deserialize<List<CatalogSampleWord>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];

    public async Task SeedAsync(IReadOnlyList<CatalogSampleWord> sample, CancellationToken ct)
    {
        await using var lease = await operations.AcquireAsync(CatalogAdminService.LockKey, ct);
        var existing = await db.CatalogWords.ToDictionaryAsync(w => w.SourceKey, ct);
        var now = time.GetUtcNow();
        var levels = new HashSet<CatalogLevel>();
        var skipped = 0;
        foreach (var s in sample)
        {
            var (fields, problem) = CatalogContent.Check(new(s.TranslationUz, s.Romanization, s.ExampleKo, s.ExampleUz, s.TrapUz, s.Theme));
            if (!Enum.TryParse<CatalogLevel>(s.Level, out var level) || fields is not { IsComplete: true })
            {
                logger.LogWarning("Sample word {Word} skipped: {Problem}", s.Word, problem ?? "level or content missing");
                skipped++;
                continue;
            }

            var pos = CatalogPos.FromUzbek(s.Pos) ?? s.Pos;
            var row = new CatalogRow(0, s.FreqRank, s.Word, s.Homonym, pos, null, level);
            if (!existing.TryGetValue(row.Key, out var word))
            {
                word = CatalogAdminService.NewWord(row, now);
                db.CatalogWords.Add(word);
            }
            else if (!CatalogDraftService.NeedsDraft(word))
            {
                continue;
            }

            (word.TranslationUz, word.Romanization, word.ExampleKo, word.ExampleUz, word.TrapUz, word.Theme) =
                (fields.TranslationUz, fields.Romanization, fields.ExampleKo, fields.ExampleUz, fields.TrapUz, fields.Theme);
            // The file is checked content: a scene in it is shown as it is (L-52); a broken one is left out.
            var (scene, _) = CatalogContent.CheckScene(s.TrapCue, s.TrapWrong, s.TrapRight, fields.TrapUz);
            (word.TrapCue, word.TrapWrong, word.TrapRight, word.TrapScenePending) = (scene?.Cue, scene?.Wrong, scene?.Right, false);
            word.Source = s.Source ?? CatalogWord.DefaultSource;
            (word.Status, word.ReviewedAt, word.UpdatedAt) = (CatalogWordStatus.Reviewed, now, now);
            levels.Add(level);
        }

        await db.SaveChangesAsync(ct);
        foreach (var level in levels)
        {
            await admin.SplitUnlockedAsync(level, ct);
            await PublishCompleteAsync(level, now, ct);
        }

        logger.LogInformation("Sample catalog seeded: {Words} words, {Skipped} skipped", sample.Count - skipped, skipped);
    }

    /// <summary>Publishes the level's units in order while all their words are checked.</summary>
    private async Task PublishCompleteAsync(CatalogLevel level, DateTimeOffset now, CancellationToken ct)
    {
        var units = await db.Units.Where(u => u.Level == level).OrderBy(u => u.Number).ToListAsync(ct);
        foreach (var unit in units.Where(u => u.Status != UnitStatus.Published))
        {
            if (unit.WordCount == 0 || await db.CatalogWords.AnyAsync(w => w.UnitId == unit.Id && w.Status != CatalogWordStatus.Reviewed, ct))
            {
                break;
            }

            (unit.Status, unit.PublishedAt) = (UnitStatus.Published, now);
            await LearningItemSync.SyncUnitAsync(db, unit, now, ct);
        }

        await db.SaveChangesAsync(ct);
    }
}
