using System.Text;
using System.Text.RegularExpressions;

namespace KoreanAiTeacher.Application.Text;

/// <summary>
/// Comparison keys for duplicate detection and answer matching (spec IMP06, PR02).
/// Only normalises what cannot change meaning: Unicode form, whitespace, case and the many
/// apostrophe look-alikes used in Uzbek Latin. Korean letters and endings are never stripped.
/// </summary>
public static partial class TextNormalizer
{
    // ' ` ´ ‘ ’ ʻ ʼ ʹ ′
    private static readonly char[] Apostrophes = ['\'', '`', '´', '‘', '’', 'ʻ', 'ʼ', 'ʹ', '′'];

    public static string Key(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        var s = text.Normalize(NormalizationForm.FormC).Trim();
        foreach (var a in Apostrophes)
        {
            s = s.Replace(a, '\'');
        }

        s = Whitespace().Replace(s, " ");
        return s.ToLowerInvariant();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
