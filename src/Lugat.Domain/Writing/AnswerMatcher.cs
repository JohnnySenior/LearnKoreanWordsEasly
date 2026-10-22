using System.Text;
using System.Text.RegularExpressions;

namespace Lugat.Domain.Writing;

public enum MatchResult
{
    Exact,
    Spelling,
    NoMatch,
}

/// <summary>
/// Compares a typed answer with the book's text without an AI call (spec PR02): case, apostrophe style, spacing and
/// punctuation never count as mistakes; a single-letter (Uzbek) or single-jamo (Korean) slip is a spelling error,
/// kept apart from a wrong meaning. Any one meaning of a multi-meaning entry is accepted.
/// </summary>
public static partial class AnswerMatcher
{
    public static MatchResult MatchUzbek(string answer, string expected)
    {
        var a = NormalizeUzbek(answer);
        if (a.Length == 0)
        {
            return MatchResult.NoMatch;
        }

        var options = UzbekOptions(expected);
        if (options.Contains(a))
        {
            return MatchResult.Exact;
        }

        return options.Any(o => o.Length >= 5 && Levenshtein(a, o) <= 1) ? MatchResult.Spelling : MatchResult.NoMatch;
    }

    public static MatchResult MatchKorean(string answer, string expected, string? note = null)
    {
        var a = NormalizeKorean(answer);
        if (a.Length == 0)
        {
            return MatchResult.NoMatch;
        }

        var options = new[] { expected, StripParentheses(expected), note is null ? null : $"{expected}{note}" }
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Select(o => NormalizeKorean(o!))
            .Where(o => o.Length > 0)
            .Distinct()
            .ToList();
        if (options.Contains(a))
        {
            return MatchResult.Exact;
        }

        var jamo = Jamo(a);
        return options.Any(o => Levenshtein(jamo, Jamo(o)) <= (o.Length >= 4 ? 2 : 1)) ? MatchResult.Spelling : MatchResult.NoMatch;
    }

    /// <summary>The meanings of an entry such as "1. ishqalamoq; 2. surtmoq, artmoq" as separate normalized options.</summary>
    public static HashSet<string> UzbekOptions(string expected)
    {
        var options = new HashSet<string>(StringComparer.Ordinal) { NormalizeUzbek(expected) };
        foreach (var part in Separators().Split(StripParentheses(expected)))
        {
            var clean = NormalizeUzbek(Numbering().Replace(part, ""));
            if (clean.Length > 0)
            {
                options.Add(clean);
            }
        }

        options.Remove("");
        return options;
    }

    public static string NormalizeUzbek(string text)
    {
        var s = text.Trim().ToLowerInvariant();
        s = Apostrophes().Replace(s, "'");
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (char.IsLetterOrDigit(c) || c == '\'')
            {
                sb.Append(c);
            }
            else if (char.IsWhiteSpace(c) || c is '-' or ',' or '.' or '!' or '?' or ';' or ':')
            {
                sb.Append(' ');
            }
        }

        return Spaces().Replace(sb.ToString(), " ").Trim();
    }

    public static string NormalizeKorean(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormC))
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    /// <summary>Hangul syllables split into jamo, so 갔어요 vs 갓어요 is one edit, not one whole syllable.</summary>
    public static string Jamo(string text)
    {
        var sb = new StringBuilder(text.Length * 3);
        foreach (var c in text)
        {
            if (c is >= '가' and <= '힣')
            {
                var index = c - 0xAC00;
                sb.Append((char)(0x1100 + index / 588));
                sb.Append((char)(0x1161 + index % 588 / 28));
                if (index % 28 != 0)
                {
                    sb.Append((char)(0x11A7 + index % 28));
                }
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    public static int Levenshtein(string a, string b)
    {
        var prev = new int[b.Length + 1];
        var cur = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            prev[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            cur[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            }

            (prev, cur) = (cur, prev);
        }

        return prev[b.Length];
    }

    private static string StripParentheses(string text) => Parentheses().Replace(text, " ").Trim();

    [GeneratedRegex("[‘’ʻʼ`´]")]
    private static partial Regex Apostrophes();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"[,;/]|\s\d+\.\s*")]
    private static partial Regex Separators();

    [GeneratedRegex(@"^\s*\d+\s*[.)]\s*")]
    private static partial Regex Numbering();

    [GeneratedRegex(@"\([^)]*\)|\[[^\]]*\]")]
    private static partial Regex Parentheses();
}
