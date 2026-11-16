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
}
