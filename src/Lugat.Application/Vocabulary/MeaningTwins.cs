using Lugat.Application.Abstractions;
using Lugat.Application.Catalog;
using Lugat.Domain.Catalog;
using Lugat.Domain.Vocabulary;
using Lugat.Domain.Writing;
using Microsoft.EntityFrameworkCore;

namespace Lugat.Application.Vocabulary;

/// <summary>
/// Words that share an Uzbek meaning, such as 안 and 아니다 "emas" (L-37). Asked from Uzbek, both are right, so the
/// prompt says which one is meant, and typing the other one is "same meaning, another word", not a mistake.
/// </summary>
public static class MeaningTwins
{
    public sealed record Twin(Guid Id, string Korean, string Uzbek, string? Pos);

    /// <summary>The other words in <paramref name="pool"/> with a meaning of <paramref name="uzbek"/>.</summary>
    public static List<Twin> Of(Guid id, string korean, string uzbek, IEnumerable<Twin> pool)
    {
        var meanings = UnitTestGenerator.Meanings(uzbek);
        var key = AnswerMatcher.NormalizeKorean(korean);
        return pool
            .Where(t => t.Id != id && AnswerMatcher.NormalizeKorean(t.Korean) != key && UnitTestGenerator.Meanings(t.Uzbek).Overlaps(meanings))
            .ToList();
    }

    /// <summary>
    /// What tells this word from its twins on an Uzbek → Korean prompt: its part of speech when that differs
    /// ("ravish"), else the twins themselves ("아니다 emas"); null when the learner has no twin of it.
    /// </summary>
    public static async Task<string?> HintAsync(IAppDbContext db, Guid ownerId, LearningItem sense, CancellationToken ct)
    {
        var twins = Of(sense.Id, sense.Korean, sense.Uzbek, await LearnerPoolAsync(db, ownerId, ct));
        if (twins.Count == 0)
        {
            return null;
        }

        var pos = sense.CatalogWordId is { } wordId
            ? await db.CatalogWords.AsNoTracking().Where(w => w.Id == wordId).Select(w => w.Pos).FirstOrDefaultAsync(ct)
            : null;
        return Hint(pos, twins);
    }

    public static string Hint(string? pos, IReadOnlyList<Twin> twins) =>
        pos is { Length: > 0 } && twins.All(t => t.Pos is { Length: > 0 } && t.Pos != pos)
            ? $"{CatalogPos.Uzbek(pos)} · shu ma’noli boshqa so‘z ham bor"
            : $"{string.Join(", ", twins.Select(t => t.Korean).Distinct())} emas";

    /// <summary>The learner's words (the units they opened) with their part of speech.</summary>
    public static async Task<List<Twin>> LearnerPoolAsync(IAppDbContext db, Guid ownerId, CancellationToken ct) =>
        await (from i in Study.LearnerItems.Of(db, ownerId)
               join w in db.CatalogWords on i.CatalogWordId equals w.Id into words
               from w in words.DefaultIfEmpty()
               select new Twin(i.Id, i.Korean, i.Uzbek, w == null ? null : w.Pos))
            .AsNoTracking()
            .ToListAsync(ct);

    /// <summary>
    /// The word typed in place of <paramref name="sense"/> when it is another word with one of its meanings, among
    /// the words a learner can meet (the catalog and their own); null otherwise.
    /// </summary>
    public static async Task<Twin?> TypedTwinAsync(IAppDbContext db, Guid ownerId, LearningItem sense, string answer, CancellationToken ct)
    {
        var typed = AnswerMatcher.NormalizeKorean(answer);
        if (typed.Length == 0)
        {
            return null;
        }

        var first = typed[..1];
        var candidates = await db.LearningItems.AsNoTracking()
            .Where(i => i.IsActive && i.Id != sense.Id && (i.OwnerId == null || i.OwnerId == ownerId) && i.Korean.StartsWith(first))
            .Select(i => new Twin(i.Id, i.Korean, i.Uzbek, null))
            .Take(500)
            .ToListAsync(ct);
        return Of(sense.Id, sense.Korean, sense.Uzbek, candidates.Where(c => AnswerMatcher.NormalizeKorean(c.Korean) == typed)).FirstOrDefault();
    }
}
