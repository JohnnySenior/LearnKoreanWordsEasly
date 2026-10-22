using System.Text;
using System.Text.RegularExpressions;

namespace Lugat.Application.Text;

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

    /// <summary>
    /// The one stored form of Uzbek Latin text (spec §2.3): o‘ and g‘ with ‘ (U+2018), the tutuq belgisi with
    /// ’ (U+2019), whatever look-alike was typed. Trims and folds runs of spaces; null stays null.
    /// </summary>
    public static string? Uzbek(string? text)
    {
        if (text is null)
        {
            return null;
        }

        var chars = text.Normalize(NormalizationForm.FormC).Trim().ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(Apostrophes, chars[i]) >= 0)
            {
                chars[i] = i > 0 && chars[i - 1] is 'o' or 'O' or 'g' or 'G' ? '‘' : '’';
            }
        }

        return Spaces().Replace(new string(chars), " ");
    }

    [GeneratedRegex(@"[ \t]+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
