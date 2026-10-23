using System.Text.RegularExpressions;
using Lugat.Application.Text;
using Lugat.Domain.Testing;

namespace Lugat.Application.Catalog;

/// <summary>A word of a unit test: what is asked, and what decides which other words may be wrong options.</summary>
/// <param name="Shown">The Korean as shown in options, with the note that tells same-written words apart.</param>
public sealed record UnitTestWord(Guid ItemId, string Korean, string Shown, string Uzbek, string Pos);

/// <summary>
/// The unit test (spec §4.4): one question per word in random order. Of 25 words, 10 Korean → Uzbek meaning, 10 Uzbek
/// → Korean word and 5 heard → Korean word (other sizes in the same proportion); listening becomes meaning when there is
/// no speech service. Wrong options come from this and earlier published units, of the same part of speech when
/// possible, and never are another right answer: not the same written word (a homonym), not a word sharing one of the
/// meanings (a synonym, 또 / 더 "yana"). Options are shuffled on the server. Each attempt draws anew.
/// </summary>
public static partial class UnitTestGenerator
{
    public const int Options = 4;

    public const string ListeningPrompt = "Eshiting va eshitgan so‘zingizni tanlang";

    /// <param name="pool">Words of this and earlier published units, the unit's own included.</param>
    public static List<TestQuestion> Build(IReadOnlyList<UnitTestWord> unit, IReadOnlyList<UnitTestWord> pool, bool listening, Random random)
    {
        var words = unit.OrderBy(_ => random.Next()).ToList();
        var listen = listening ? (int)Math.Round(words.Count / 5.0, MidpointRounding.AwayFromZero) : 0;
        var meaning = (words.Count - listen + 1) / 2;
        var questions = new List<TestQuestion>();
        for (var i = 0; i < words.Count; i++)
        {
            var kind = i < meaning ? TestQuestionKind.WordMeaning
                : i < words.Count - listen ? TestQuestionKind.WordKorean
                : TestQuestionKind.WordListening;
            questions.Add(Question(kind, words[i], pool, random));
        }

        return questions.OrderBy(_ => random.Next()).Select((q, i) => q with { Index = i }).ToList();
    }

    private static TestQuestion Question(TestQuestionKind kind, UnitTestWord w, IReadOnlyList<UnitTestWord> pool, Random random)
    {
        var wrong = Distractors(w, pool, random);
        return kind switch
        {
            TestQuestionKind.WordMeaning => new TestQuestion(0, kind, w.Shown, null, null,
                Shuffle([w.Uzbek, .. wrong.Select(x => x.Uzbek)], random), w.Uzbek, null, null, w.ItemId, false),
            TestQuestionKind.WordKorean => new TestQuestion(0, kind, w.Uzbek, null, null,
                Shuffle([w.Shown, .. wrong.Select(x => x.Shown)], random), w.Shown, null, null, w.ItemId, false),
            _ => new TestQuestion(0, kind, ListeningPrompt, null, w.Uzbek,
                Shuffle([w.Shown, .. wrong.Select(x => x.Shown)], random), w.Shown, w.Korean, null, w.ItemId, false),
        };
    }

    /// <summary>Up to three words that are surely wrong for <paramref name="w"/>; the same part of speech first.</summary>
    public static List<UnitTestWord> Distractors(UnitTestWord w, IReadOnlyList<UnitTestWord> pool, Random random)
    {
        var meanings = Meanings(w.Uzbek);
        var korean = TextNormalizer.Key(w.Korean);
        var shown = new HashSet<string> { TextNormalizer.Key(w.Shown) };
        var uzbek = new HashSet<string> { TextNormalizer.Key(w.Uzbek) };
        return pool
            .Where(o => o.ItemId != w.ItemId && TextNormalizer.Key(o.Korean) != korean && !Meanings(o.Uzbek).Overlaps(meanings))
            .OrderBy(o => o.Pos == w.Pos ? 0 : 1).ThenBy(_ => random.Next())
            .Where(o => shown.Add(TextNormalizer.Key(o.Shown)) & uzbek.Add(TextNormalizer.Key(o.Uzbek)))
            .Take(Options - 1)
            .ToList();
    }

    /// <summary>"bor bo‘lmoq; -da bo‘lmoq" → {"bor bo'lmoq", "-da bo'lmoq"}; "men (do‘stona)" counts as "men".</summary>
    public static HashSet<string> Meanings(string uzbek) =>
        uzbek.Split([';', ','], StringSplitOptions.RemoveEmptyEntries)
            .Select(m => TextNormalizer.Key(Parenthesis().Replace(m, " ")))
            .Where(m => m.Length > 0)
            .ToHashSet();

    private static List<string> Shuffle(List<string> items, Random random) => items.OrderBy(_ => random.Next()).ToList();

    [GeneratedRegex(@"\([^)]*\)")]
    private static partial Regex Parenthesis();
}
