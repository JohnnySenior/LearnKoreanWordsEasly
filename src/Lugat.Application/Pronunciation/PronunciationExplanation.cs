using System.Text.RegularExpressions;

namespace Lugat.Application.Pronunciation;

/// <summary>Known parenthesized rule labels, including legacy AI notes. Sound examples are left intact.</summary>
public static class PronunciationExplanation
{
    private static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        ["거센소리되기"] = "undoshning kuchli nafas bilan aytilishi",
        ["격음화"] = "undoshning kuchli nafas bilan aytilishi",
        ["된소리되기"] = "undoshning tarangroq aytilishi",
        ["경음화"] = "undoshning tarangroq aytilishi",
        ["비음화"] = "undoshning burun tovushiga aylanishi",
        ["유음화"] = "undoshning ㄹ tovushiga aylanishi",
        ["구개음화"] = "ㄷ yoki ㅌ ning ㅈ yoki ㅊ kabi aytilishi",
        ["연음"] = "oxirgi undoshning keyingi bo‘g‘inga ulanib aytilishi",
        ["자음군단순화"] = "oxirgi ikki undoshdan bittasining aytilishi",
        ["음절의 끝소리 규칙"] = "bo‘g‘in oxiridagi undoshning o‘zgarishi",
        ["ㅎ 탈락"] = "ㅎ tovushining tushib qolishi",
        ["ㄴ 첨가"] = "ㄴ tovushining qo‘shilishi",
    };

    public static string? Readable(string? explanation) => explanation is null ? null :
        Regex.Replace(explanation, @"\(\s*([^()]+?)\s*\)", match =>
            Labels.TryGetValue(match.Groups[1].Value.Trim(), out var uzbek) ? $"({uzbek})" : match.Value);

    public static bool HasLatinExplanation(string explanation) => Regex.IsMatch(explanation, "[A-Za-z]{2,}");

    /// <summary>Legacy Korean prose is replaced by a factual Uzbek note, not a guessed translation.</summary>
    public static string? ForWord(string korean, string? reading, string? explanation)
    {
        var note = Readable(explanation);
        if (note is null || !HasKoreanProse(note)) return note;
        if (string.IsNullOrWhiteSpace(reading)) return null;
        if (reading == korean) return "So‘z yozilganidek o‘qiladi.";
        var rule = IsSimpleLiaison(korean, reading)
            ? "Bo‘g‘in oxiridagi undosh keyingi unliga ulanib o‘qiladi. " : "";
        return rule + $"«{korean}» so‘zi «{reading}» deb o‘qiladi.";
    }

    private static bool HasKoreanProse(string note)
    {
        // Preserve Uzbek explanations with Korean examples. This is a conservative script heuristic.
        var prose = Regex.Replace(note, "[«“‘’\"][가-힣ㄱ-ㅎㅏ-ㅣ]+[»”‘’\"]", "");
        return Regex.Matches(prose, "[가-힣]{2,}").Count > Regex.Matches(prose, "[A-Za-z]{2,}").Count;
    }

    private static bool IsSimpleLiaison(string korean, string reading)
    {
        // Recognize only an exact, complete match of single final consonants moving before ㅇ.
        // Clusters, ㅎ/ㅇ finals and any additional sound change fall back to the factual reading note.
        var result = korean.ToCharArray();
        var changed = false;
        for (var i = 0; i < result.Length - 1; i++)
        {
            if (result[i] is < '가' or > '힣' || result[i + 1] is < '가' or > '힣') continue;
            var first = result[i] - '가';
            var next = result[i + 1] - '가';
            if (next / 588 != 11) continue; // ㅇ before a vowel
            var onset = (first % 28) switch
            {
                1 => 0, 2 => 1, 4 => 2, 7 => 3, 8 => 5, 16 => 6, 17 => 7,
                19 => 9, 20 => 10, 22 => 12, 23 => 14, 24 => 15, 25 => 16, 26 => 17,
                _ => -1,
            };
            if (onset < 0) continue;
            result[i] = (char)(result[i] - first % 28);
            result[i + 1] = (char)(result[i + 1] + (onset - 11) * 588);
            changed = true;
        }
        return changed && new string(result) == reading;
    }
}
