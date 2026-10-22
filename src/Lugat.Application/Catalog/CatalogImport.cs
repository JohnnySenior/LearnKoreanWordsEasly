using System.Globalization;
using System.Text.RegularExpressions;
using Lugat.Domain.Catalog;

namespace Lugat.Application.Catalog;

/// <summary>Reads the cells of the first sheet of an uploaded file (Excel .xls/.xlsx, or the text version).</summary>
public interface ICatalogFileReader
{
    /// <exception cref="ValidationException">The file is not a spreadsheet or text table that can be read.</exception>
    IReadOnlyList<IReadOnlyList<string?>> ReadRows(Stream content, string fileName);
}

/// <summary>One row of the official list, as read from the file.</summary>
public sealed record CatalogRow(int Line, int Rank, string Word, string? Homonym, string Pos, string? Gloss, CatalogLevel Level)
{
    public string Key => CatalogWord.Key(Level, Word, Homonym, Pos);
}

/// <summary>The rows that could be read, and why the others could not.</summary>
public sealed record CatalogRows(IReadOnlyList<CatalogRow> Rows, int DataRows, IReadOnlyList<string> Problems);

/// <summary>
/// Understands the layout of 국립국어원 「한국어 학습용 어휘 목록」 (spec §2.1): a header row with 순위 (rank),
/// 단어 (word, homonym number glued on: 가다01), 품사 (part of speech), 풀이 (gloss) and 등급 (A/B/C).
/// Columns are found by their header, so their order and any extra columns do not matter. The text version may be
/// split on spaces (L-22): a gloss with spaces is joined again and a missing gloss is allowed, as long as the gloss
/// column stands between the part of speech and the level. A file without a header is read in the official order
/// 순위 단어 품사 풀이 등급 when its rows clearly look like that (a number first, A/B/C last).
/// </summary>
public static partial class CatalogImport
{
    /// <summary>How many skipped rows are described back to the admin.</summary>
    public const int MaxProblems = 20;

    public static CatalogRows Parse(IReadOnlyList<IReadOnlyList<string?>> cells)
    {
        var headerAt = -1;
        int rank = -1, word = -1, pos = -1, gloss = -1, level = -1;
        for (var r = 0; r < Math.Min(cells.Count, 20) && headerAt < 0; r++)
        {
            var row = cells[r];
            int Find(params string[] names) => row.Select((c, i) => (Cell: Clean(c), i)).FirstOrDefault(x => x.Cell is { } t && names.Any(t.Contains), (null, -1)).i;
            (rank, word, pos, gloss, level) = (Find("순위"), Find("단어", "어휘"), Find("품사"), Find("풀이"), Find("등급"));
            if (rank >= 0 && word >= 0 && pos >= 0 && level >= 0)
            {
                headerAt = r;
            }
        }

        var width = headerAt >= 0 ? Trimmed(cells[headerAt]).Count : 5;
        if (headerAt < 0)
        {
            if (!LooksLikeOfficialOrder(cells))
            {
                throw new ValidationException("Faylda «순위», «단어», «품사», «등급» ustunlari topilmadi. Rasmiy ro‘yxat faylini yuklang.");
            }

            (rank, word, pos, gloss, level) = (0, 1, 2, 3, 4);
        }

        // Spaces in the gloss, or no gloss, shift the cells of a space-separated line: put them back.
        var realign = gloss >= 0 && rank < gloss && word < gloss && pos < gloss && level > gloss;

        var rows = new List<CatalogRow>();
        var problems = new List<string>();
        var dataRows = 0;
        var seen = new HashSet<string>();
        for (var r = headerAt + 1; r < cells.Count; r++)
        {
            var row = Trimmed(cells[r]);
            if (realign && row.Count != width && row.Count >= width - 1)
            {
                var after = width - gloss - 1;
                var middle = row.Skip(gloss).Take(row.Count - after - gloss).Select(Clean).OfType<string>().ToList();
                row = [.. row.Take(gloss), middle.Count == 0 ? null : string.Join(" ", middle), .. row.Skip(row.Count - after)];
            }

            string? At(int i) => i >= 0 && i < row.Count ? Clean(row[i]) : null;
            var (rankText, wordText, posText, glossText, levelText) = (At(rank), At(word), At(pos), At(gloss), At(level));
            if (rankText is null && wordText is null && posText is null && levelText is null)
            {
                continue; // empty line
            }

            dataRows++;
            var line = r + 1;
            string? problem = null;
            if (!int.TryParse(rankText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rankValue) || rankValue <= 0)
            {
                problem = "순위 son emas";
            }
            else if (wordText is null || WordWithHomonym().Match(wordText) is not { Success: true } m)
            {
                problem = "단어 bo‘sh";
            }
            else if (posText is null)
            {
                problem = "품사 bo‘sh";
            }
            else if (levelText is not { Length: 1 } || !char.IsAsciiLetter(levelText[0])
                || !Enum.TryParse<CatalogLevel>(levelText, ignoreCase: true, out var levelValue))
            {
                problem = "등급 A, B yoki C emas";
            }
            else
            {
                var item = new CatalogRow(line, rankValue, m.Groups["word"].Value.Trim(), m.Groups["homonym"].Success ? m.Groups["homonym"].Value : null, posText, glossText, levelValue);
                if (seen.Add(item.Key))
                {
                    rows.Add(item);
                    continue;
                }

                problem = "takrorlangan so‘z";
            }

            if (problems.Count < MaxProblems)
            {
                problems.Add($"{line}-qator: {problem}.");
            }
        }

        return new CatalogRows(rows, dataRows, problems);
    }

    private static string? Clean(string? cell) => string.IsNullOrWhiteSpace(cell) ? null : cell.Trim();

    /// <summary>The cells without empty ones at the end of the line.</summary>
    private static List<string?> Trimmed(IReadOnlyList<string?> row)
    {
        var count = row.Count;
        while (count > 0 && Clean(row[count - 1]) is null)
        {
            count--;
        }

        return row.Take(count).ToList();
    }

    /// <summary>Most non-empty lines start with a rank and end with a level: the official order without a header.</summary>
    private static bool LooksLikeOfficialOrder(IReadOnlyList<IReadOnlyList<string?>> cells)
    {
        var lines = cells.Select(Trimmed).Where(r => r.Count > 0).Take(50).ToList();
        var matching = lines.Count(r => r.Count >= 4 && int.TryParse(Clean(r[0]), NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            && Clean(r[^1]) is "A" or "B" or "C");
        return lines.Count > 0 && matching >= lines.Count * 0.8;
    }

    /// <summary>"가다01" → 가다 + 01; "-가" or "가" without a number stays as it is.</summary>
    [GeneratedRegex(@"^(?<word>.*?\D)(?<homonym>\d{2})?$")]
    private static partial Regex WordWithHomonym();
}
