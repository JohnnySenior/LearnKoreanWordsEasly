using System.Text;
using Lugat.Application.Abstractions;
using Lugat.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Lugat.Application.Seo;

public sealed record SeoLink(string Slug, string Word, string Translation);

public sealed record SeoWord(
    string Slug,
    string Word,
    string? Romanization,
    string Translation,
    string PartOfSpeech,
    CatalogLevel Level,
    string? ExampleKo,
    string? ExampleUz,
    Guid UnitId,
    int UnitNumber,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<SeoLink> Related);

public sealed record SeoLevelGroup(CatalogLevel Level, IReadOnlyList<SeoLink> Words);

public sealed record SeoListPage(int Page, int Pages, int Total, IReadOnlyList<SeoLevelGroup> Groups);

public sealed record SeoSitemapEntry(string Slug, DateTimeOffset LastModified);

/// <summary>
/// Public word pages for search engines (L-56): only words a learner could meet, i.e. reviewed words of a published
/// unit. Gives each such word a lasting slug the first time it is public. No learner data is read here.
/// </summary>
public sealed class SeoService(IAppDbContext db, IOperationLock operations, TimeProvider time)
{
    public const int PageSize = 500;

    public const int RelatedCount = 8;

    // A class with init members, not a positional record: EF Core can filter on its members after the projection.
    private sealed class Row
    {
        public Guid Id { get; init; }
        public string Word { get; init; } = "";
        public string? Homonym { get; init; }
        public string Pos { get; init; } = "";
        public string? Romanization { get; init; }
        public string Translation { get; init; } = "";
        public CatalogLevel Level { get; init; }
        public int? FreqRank { get; init; }
        public string? ExampleKo { get; init; }
        public string? ExampleUz { get; init; }
        public Guid UnitId { get; init; }
        public int UnitNumber { get; init; }
        public int? OrderInUnit { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }
    }

    private IQueryable<Row> Public() =>
        from w in db.CatalogWords
        join u in db.Units on w.UnitId equals u.Id
        where w.Status == CatalogWordStatus.Reviewed && u.Status == UnitStatus.Published && w.TranslationUz != null
        select new Row
        {
            Id = w.Id, Word = w.Word, Homonym = w.Homonym, Pos = w.Pos, Romanization = w.Romanization, Translation = w.TranslationUz!,
            Level = w.Level, FreqRank = w.FreqRank, ExampleKo = w.ExampleKo, ExampleUz = w.ExampleUz, UnitId = u.Id, UnitNumber = u.Number,
            OrderInUnit = w.OrderInUnit, UpdatedAt = w.UpdatedAt,
        };

    public async Task<SeoWord?> WordAsync(string slug, CancellationToken ct)
    {
        await EnsureSlugsAsync(ct);
        var id = await db.WordSlugs.Where(s => s.Slug == slug).Select(s => (Guid?)s.CatalogWordId).SingleOrDefaultAsync(ct);
        if (id is null || await Public().Where(r => r.Id == id).AsNoTracking().SingleOrDefaultAsync(ct) is not { } word)
        {
            return null;
        }

        var siblings = await Public().Where(r => r.UnitId == word.UnitId && r.Id != word.Id).AsNoTracking().ToListAsync(ct);
        var slugs = await SlugsAsync(siblings.Select(s => s.Id), ct);
        // The words around it in the unit, so every page links on to its neighbours.
        var related = siblings.Where(s => slugs.ContainsKey(s.Id))
            .OrderBy(s => Math.Abs((s.OrderInUnit ?? 0) - (word.OrderInUnit ?? 0))).ThenBy(s => s.OrderInUnit)
            .Take(RelatedCount).OrderBy(s => s.OrderInUnit)
            .Select(s => new SeoLink(slugs[s.Id], s.Word, s.Translation)).ToList();
        return new SeoWord(slug, word.Word, word.Romanization, word.Translation, CatalogPos.Uzbek(word.Pos), word.Level,
            word.ExampleKo, word.ExampleUz, word.UnitId, word.UnitNumber, word.UpdatedAt, related);
    }

    public async Task<SeoListPage> ListAsync(int page, CancellationToken ct)
    {
        await EnsureSlugsAsync(ct);
        var rows = await Public().AsNoTracking().ToListAsync(ct);
        var slugs = await SlugsAsync(rows.Select(r => r.Id), ct);
        var ordered = rows.Where(r => slugs.ContainsKey(r.Id))
            .OrderBy(r => r.Level).ThenBy(r => r.UnitNumber).ThenBy(r => r.OrderInUnit).ToList();
        var pages = Math.Max(1, (ordered.Count + PageSize - 1) / PageSize);
        page = Math.Clamp(page, 1, pages);
        var groups = ordered.Skip((page - 1) * PageSize).Take(PageSize)
            .GroupBy(r => r.Level)
            .Select(g => new SeoLevelGroup(g.Key, g.Select(r => new SeoLink(slugs[r.Id], r.Word, r.Translation)).ToList()))
            .ToList();
        return new SeoListPage(page, pages, ordered.Count, groups);
    }

    public async Task<IReadOnlyList<SeoSitemapEntry>> SitemapAsync(CancellationToken ct)
    {
        await EnsureSlugsAsync(ct);
        var rows = await Public().Select(r => new { r.Id, r.UpdatedAt, r.Level, r.FreqRank }).AsNoTracking().ToListAsync(ct);
        var slugs = await SlugsAsync(rows.Select(r => r.Id), ct);
        return rows.Where(r => slugs.ContainsKey(r.Id)).OrderBy(r => r.Level).ThenBy(r => r.FreqRank ?? int.MaxValue)
            .Select(r => new SeoSitemapEntry(slugs[r.Id], r.UpdatedAt)).ToList();
    }

    private async Task<Dictionary<Guid, string>> SlugsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.ToList();
        return await db.WordSlugs.Where(s => list.Contains(s.CatalogWordId)).ToDictionaryAsync(s => s.CatalogWordId, s => s.Slug, ct);
    }

    /// <summary>
    /// Gives a slug to every public word that has none. The most frequent word gets the plain romanization; a later
    /// word with the same one gets its homonym number, then its part of speech, then a number. Slugs are never changed.
    /// </summary>
    public async Task EnsureSlugsAsync(CancellationToken ct)
    {
        if (!await Public().AnyAsync(r => !db.WordSlugs.Any(s => s.CatalogWordId == r.Id), ct))
        {
            return;
        }

        await using var lease = await operations.AcquireAsync("word-slugs", ct);
        var missing = await Public().Where(r => !db.WordSlugs.Any(s => s.CatalogWordId == r.Id))
            .OrderBy(r => r.Level).ThenBy(r => r.FreqRank ?? int.MaxValue).ThenBy(r => r.Word).AsNoTracking().ToListAsync(ct);
        if (missing.Count == 0)
        {
            return;
        }

        var taken = (await db.WordSlugs.Select(s => s.Slug).ToListAsync(ct)).ToHashSet();
        var now = time.GetUtcNow();
        foreach (var word in missing)
        {
            var slug = Pick(word, taken);
            taken.Add(slug);
            db.WordSlugs.Add(new WordSlug { CatalogWordId = word.Id, Slug = slug, CreatedAt = now });
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Another instance gave them at the same moment: theirs stand.
            db.DiscardChanges();
        }
    }

    private static string Pick(Row word, HashSet<string> taken)
    {
        var plain = Slugify(word.Romanization);
        var basis = plain.Length > 0 ? plain : "soz";
        var candidates = new List<string> { basis };
        if (!string.IsNullOrWhiteSpace(word.Homonym))
        {
            candidates.Add($"{basis}-{Slugify(word.Homonym)}");
        }

        var pos = Slugify(CatalogPos.Uzbek(word.Pos));
        if (pos.Length > 0)
        {
            candidates.Add($"{basis}-{pos}");
        }

        foreach (var candidate in candidates.Where(c => c.Length > 0))
        {
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }

        for (var n = 2; ; n++)
        {
            if (!taken.Contains($"{basis}-{n}"))
            {
                return $"{basis}-{n}";
            }
        }
    }

    /// <summary>Lower-case Latin letters, digits and single hyphens: "Sa-rang" → "sa-rang", "fe’l" → "fel".</summary>
    public static string Slugify(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        var plain = new StringBuilder();
        foreach (var c in text.Normalize(NormalizationForm.FormD).ToLowerInvariant())
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                plain.Append(c);
            }
            else if (c is '\'' or '’' or '‘' or 'ʻ' or 'ʼ' or '`' || System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                // Apostrophes and accents vanish: "o‘" → "o".
            }
            else if (plain.Length > 0 && plain[^1] != '-')
            {
                plain.Append('-');
            }
        }

        var slug = plain.ToString().Trim('-');
        return slug.Length > 80 ? slug[..80].TrimEnd('-') : slug;
    }
}
