using System.Text.RegularExpressions;
using Lugat.Application.Ai;
using Lugat.Application.Text;

namespace Lugat.Application.Catalog;

/// <summary>The content fields of a catalog word (spec §2.3), as typed by an editor or written by the AI.</summary>
public sealed record CatalogContentFields(
    string? TranslationUz,
    string? Romanization,
    string? ExampleKo,
    string? ExampleUz,
    string? TrapUz,
    string? Theme)
{
    /// <summary>Everything a learner's card needs; only complete words can be marked as checked.</summary>
    public bool IsComplete => TranslationUz is not null && Romanization is not null && ExampleKo is not null && ExampleUz is not null;
}

/// <summary>The trap acted out on the card (L-52): an optional Uzbek situation, the Uzbek speaker's Korean, the right Korean.</summary>
public sealed record TrapScene(string? Cue, string Wrong, string Right);

/// <summary>
/// The rules of spec §2.3 in one place, for editors and for AI drafts alike: Uzbek in Latin script with one
/// apostrophe form, 1-3 meanings separated by ";", Revised Romanization in Latin letters, one Korean example of at
/// most 8 words (어절) with its Uzbek translation, a trap only when there is one, a short theme.
/// </summary>
public static partial class CatalogContent
{
    public const int MaxMeanings = 3;

    public const int MaxExampleWords = 8;

    /// <summary>Cleans the fields (blank → null, one apostrophe form) and returns them, or the first problem in Uzbek.</summary>
    public static (CatalogContentFields? Fields, string? Problem) Check(CatalogContentFields input)
    {
        var translation = Blank(TextNormalizer.Uzbek(input.TranslationUz));
        if (translation is not null)
        {
            var meanings = translation.Split(';', StringSplitOptions.TrimEntries);
            if (meanings.Length > MaxMeanings || meanings.Any(m => m.Length is 0 or > 100) || !SentenceCheckService.IsUzbekLatin(translation))
            {
                return (null, "Tarjima o‘zbekcha lotin yozuvida bo‘lsin: 1–3 ma’no, «;» bilan ajratilgan.");
            }

            translation = string.Join("; ", meanings);
        }

        var romanization = Blank(input.Romanization);
        if (romanization is not null && (romanization.Length > 100 || !Romanized().IsMatch(romanization)))
        {
            return (null, "Lotincha o‘qilishi faqat lotin harflarida bo‘lsin (masalan, hada).");
        }

        var exampleKo = Blank(input.ExampleKo);
        if (exampleKo is not null && (exampleKo.Length > 200 || !SentenceCheckService.ContainsHangul(exampleKo) || Words(exampleKo) > MaxExampleWords))
        {
            return (null, $"Misol koreyscha (Hangul) bo‘lsin va {MaxExampleWords} so‘zdan oshmasin.");
        }

        var exampleUz = Blank(TextNormalizer.Uzbek(input.ExampleUz));
        if (exampleUz is not null && (exampleUz.Length > 300 || !SentenceCheckService.IsUzbekLatin(exampleUz)))
        {
            return (null, "Misolning tarjimasi o‘zbekcha lotin yozuvida bo‘lsin.");
        }

        if ((exampleKo is null) != (exampleUz is null))
        {
            return (null, "Misol va uning tarjimasi birga to‘ldirilsin.");
        }

        var trap = Blank(TextNormalizer.Uzbek(input.TrapUz));
        if (trap is not null && (trap.Length > 1000 || !SentenceCheckService.IsUzbekLatin(trap)))
        {
            return (null, "Tuzoq izohi o‘zbekcha lotin yozuvida bo‘lsin (1000 belgigacha).");
        }

        var theme = Blank(TextNormalizer.Uzbek(input.Theme));
        if (theme is not null && (theme.Length > 40 || !SentenceCheckService.IsUzbekLatin(theme)))
        {
            return (null, "Mavzu qisqa (40 belgigacha) va lotin yozuvida bo‘lsin.");
        }

        return (new CatalogContentFields(translation, romanization, exampleKo, exampleUz, trap, theme), null);
    }

    public const int MaxSceneKorean = 60;

    public const int MaxSceneCue = 120;

    /// <summary>
    /// Checks a trap scene (L-52): all blank is no scene; otherwise both Korean lines in Hangul, different, short,
    /// and a trap note to explain them. Returns the cleaned scene or the problem in Uzbek.
    /// </summary>
    public static (TrapScene? Scene, string? Problem) CheckScene(string? cue, string? wrong, string? right, string? trapUz)
    {
        var (c, w, r) = (Blank(TextNormalizer.Uzbek(cue)), Blank(wrong), Blank(right));
        if (c is null && w is null && r is null)
        {
            return (null, null);
        }

        if (w is null || r is null)
        {
            return (null, "Tuzoq sahnasida xato va to‘g‘ri variant birga to‘ldirilsin (yoki ikkalasi ham bo‘sh).");
        }

        if (Blank(trapUz) is null)
        {
            return (null, "Tuzoq sahnasi tuzoq izohi bilan birga bo‘ladi: avval izohni yozing.");
        }

        if (w.Length > MaxSceneKorean || r.Length > MaxSceneKorean || !SentenceCheckService.ContainsHangul(w) || !SentenceCheckService.ContainsHangul(r))
        {
            return (null, $"Xato va to‘g‘ri variant koreyscha (Hangul) bo‘lsin, {MaxSceneKorean} belgigacha.");
        }

        if (string.Equals(w.Replace(" ", ""), r.Replace(" ", ""), StringComparison.Ordinal))
        {
            return (null, "Xato va to‘g‘ri variant bir xil bo‘lmasin.");
        }

        if (c is not null && (c.Length > MaxSceneCue || !SentenceCheckService.IsUzbekLatin(c)))
        {
            return (null, $"Vaziyat o‘zbekcha lotin yozuvida bo‘lsin ({MaxSceneCue} belgigacha).");
        }

        return (new TrapScene(c, w, r), null);
    }

    /// <summary>Word count (어절): Korean words are separated by spaces.</summary>
    public static int Words(string korean) => korean.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z\- ]*$")]
    private static partial Regex Romanized();
}
