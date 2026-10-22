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
/// Columns are found by their header, so their order and any extra columns do not matter.
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

        if (headerAt < 0)
        {
            throw new ValidationException("Faylda «순위», «단어», «품사», «등급» ustunlari topilmadi. Rasmiy ro‘yxat faylini yuklang.");
        }

        var rows = new List<CatalogRow>();
        var problems = new List<string>();
        var dataRows = 0;
        var seen = new HashSet<string>();
        for (var r = headerAt + 1; r < cells.Count; r++)
        {
            var row = cells[r];
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

    /// <summary>"가다01" → 가다 + 01; "-가" or "가" without a number stays as it is.</summary>
    [GeneratedRegex(@"^(?<word>.*?\D)(?<homonym>\d{2})?$")]
    private static partial Regex WordWithHomonym();
}
