using System.Text.RegularExpressions;
using Lugat.Application.Ai;
using Lugat.Application.Catalog;
using Lugat.Domain.Teaching;

namespace Lugat.Application.Teaching;

public sealed record GroupDeckRow(int Line, string Korean, string Uzbek);

/// <summary>The rows that could be read, how many rows had text, and why the others were skipped.</summary>
public sealed record GroupDeckRows(IReadOnlyList<GroupDeckRow> Rows, int DataRows, IReadOnlyList<string> Problems);

/// <summary>
/// Reads a teacher's two-column list (L-67): Korean in one cell, the Uzbek translation in another, in any order and
/// with any extra columns (numbers, a part of speech). A single cell "학교 - maktab" or "1. 학교: maktab" is split too.
/// A first line without Hangul is a header and is skipped quietly; later such lines are reported.
/// </summary>
public static partial class GroupDeckImport
{
    public const int MaxProblems = 20;

    public static GroupDeckRows Parse(IReadOnlyList<IReadOnlyList<string?>> cells)
    {
        var rows = new List<GroupDeckRow>();
        var problems = new List<string>();
        var seen = new HashSet<string>();
        var dataRows = 0;
        void Problem(int line, string why)
        {
            if (problems.Count < MaxProblems)
            {
                problems.Add($"{line}-qator: {why}");
            }
        }

        for (var r = 0; r < cells.Count; r++)
        {
            var parts = cells[r].Select(Clean).OfType<string>().SelectMany(Split).Select(Clean).OfType<string>().ToList();
            if (parts.Count == 0)
            {
                continue;
            }

            var line = r + 1;
            var korean = parts.FirstOrDefault(SentenceCheckService.ContainsHangul);
            if (korean is null)
            {
                if (dataRows > 0 || r > 0)
                {
                    dataRows++;
                    Problem(line, "koreyscha so‘z topilmadi");
                }

                continue; // the header line
            }

            dataRows++;
            korean = Numbering().Replace(korean, "").Trim();
            var uzbek = string.Join(" ", parts.Where(p => !ReferenceEquals(p, korean) && !SentenceCheckService.ContainsHangul(p) && !Filler().IsMatch(p)));
            if (korean.Length is 0 or > GroupDeckWord.MaxKorean)
            {
                Problem(line, $"koreyscha so‘z {GroupDeckWord.MaxKorean} belgidan oshmasin");
                continue;
            }

            var (fields, problem) = CatalogContent.Check(new(uzbek, null, null, null, null, null));
            if (fields?.TranslationUz is null)
            {
                Problem(line, uzbek.Length == 0 ? "o‘zbekcha tarjima yo‘q" : problem ?? "tarjima o‘qilmadi");
                continue;
            }

            if (!seen.Add(korean))
            {
                Problem(line, $"takrorlangan so‘z ({korean})");
                continue;
            }

            rows.Add(new GroupDeckRow(line, korean, fields.TranslationUz));
        }

        return new GroupDeckRows(rows, dataRows, problems);
    }

    /// <summary>"학교 - maktab", "학교: maktab", "학교 — maktab" in one cell → two parts; a Korean cell with a note stays whole.</summary>
    private static IEnumerable<string> Split(string cell)
    {
        if (!SentenceCheckService.ContainsHangul(cell))
        {
            return [cell];
        }

        var m = Separator().Match(cell);
        if (!m.Success)
        {
            return [cell];
        }

        var left = cell[..m.Index];
        var right = cell[(m.Index + m.Length)..];
        // Split only where one side is Korean and the other is not: "학교 - 학생" is not a translation.
        return SentenceCheckService.ContainsHangul(left) != SentenceCheckService.ContainsHangul(right) ? [left, right] : [cell];
    }

    private static string? Clean(string? value) => value?.Trim().Trim('"', '\'', '“', '”').Trim() is { Length: > 0 } v ? v : null;

    [GeneratedRegex(@"\s+[-–—:=]\s+|\s*[:=]\s+|\t")]
    private static partial Regex Separator();

    /// <summary>A number or bullet before the word: "1.", "1)", "12 -", "•".</summary>
    [GeneratedRegex(@"^\s*(\d+\s*[.)\-–]?|[•·\-–—])\s*")]
    private static partial Regex Numbering();

    /// <summary>Cells that are only a number, a dash or a part of speech in Korean brackets carry no translation.</summary>
    [GeneratedRegex(@"^(\d+[.)]?|[-–—•·]+|\(.*\)|\[.*\])$")]
    private static partial Regex Filler();
}
