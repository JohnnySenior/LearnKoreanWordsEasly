using System.Globalization;
using System.Text.RegularExpressions;
using KoreanAiTeacher.Application.Text;
using KoreanAiTeacher.Domain.Imports;

namespace KoreanAiTeacher.Application.Imports;

/// <summary>A word as positioned on a PDF page. <see cref="Baseline"/> uses PDF coordinates (grows upwards).</summary>
public sealed record LayoutWord(string Text, double Left, double Right, double Baseline);

public sealed record LayoutPage(int PdfPage, IReadOnlyList<LayoutWord> Words);

public sealed record ParsedEntry(
    int PdfPage,
    int? PrintedPage,
    int TableNumber,
    int OrderInTable,
    int OrderInDocument,
    string RawText,
    string Korean,
    string? KoreanNote,
    string Uzbek,
    EntryWarnings Warnings);

public sealed record VocabularyParseResult(IReadOnlyList<ParsedEntry> Entries, IReadOnlyList<int> SkippedPages);

/// <summary>
/// Reads a two-column vocabulary list: Korean headword on the left, Uzbek translation on the right
/// (the layout of the F-TOPIK dictionary, spec S01). Works on word positions, not on reflowed text,
/// so a translation wrapped over several printed lines is attached to the headword it sits next to.
/// Anything it is not sure about is flagged for review instead of guessed (spec IMP04, IMP05).
/// </summary>
public static partial class VocabularyLayoutParser
{
    public const string Version = "vocab-columns-v1";

    /// <summary>Words closer than this (pt) on one line belong to the same cell.</summary>
    private const double CellGap = 12;

    /// <summary>Words whose baselines differ by at most this (pt) are on the same printed line.</summary>
    private const double LineTolerance = 1.5;

    /// <summary>
    /// A translation line further than this (pt) from every headword is not attached automatically.
    /// Rows in the book are ~21pt apart, so half a row plus a little slack.
    /// </summary>
    private const double AttachDistance = 12;

    /// <summary>Pages with fewer headwords than this (cover, credits) are not word tables.</summary>
    private const int MinRowsPerTable = 5;

    public static VocabularyParseResult Parse(IEnumerable<LayoutPage> pages)
    {
        var entries = new List<ParsedEntry>();
        var skipped = new List<int>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var table = 0;

        foreach (var page in pages.OrderBy(p => p.PdfPage))
        {
            var rows = ParsePage(page, out var printedPage);
            if (rows.Count < MinRowsPerTable)
            {
                skipped.Add(page.PdfPage);
                continue;
            }

            table++;
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var warnings = row.Warnings;
                if (!seen.Add(TextNormalizer.Key(row.Korean)))
                {
                    warnings |= EntryWarnings.PossibleDuplicate;
                }

                entries.Add(new ParsedEntry(
                    page.PdfPage,
                    printedPage,
                    table,
                    i + 1,
                    entries.Count + 1,
                    row.RawText,
                    row.Korean,
                    row.KoreanNote,
                    row.Uzbek,
                    warnings));
            }
        }

        return new VocabularyParseResult(entries, skipped);
    }

    private sealed record Line(double Baseline, IReadOnlyList<LayoutWord> Words)
    {
        public string Text => string.Join(' ', Words.Select(w => w.Text));
    }

    private sealed record Cell(double Baseline, double Left, string Text, Line Line);

    private sealed record Row(string RawText, string Korean, string? KoreanNote, string Uzbek, EntryWarnings Warnings);

    private sealed class RowBuilder(Cell headword)
    {
        public Cell Headword { get; } = headword;

        public List<Cell> Translation { get; } = [];

        public bool HasStrayText { get; set; }
    }

    private static List<Row> ParsePage(LayoutPage page, out int? printedPage)
    {
        printedPage = null;
        var lines = GroupLines(page.Words);
        if (lines.Count == 0)
        {
            return [];
        }

        // A trailing "NAME NAME 12" line at the very bottom is the page footer.
        var last = lines[^1];
        var footer = FooterPattern().Match(last.Text);
        if (footer.Success)
        {
            printedPage = int.Parse(footer.Groups[1].Value, CultureInfo.InvariantCulture);
            lines.RemoveAt(lines.Count - 1);
        }

        var cells = lines.SelectMany(SplitCells).ToList();
        var headwordCells = cells.Where(c => ContainsHangul(c.Text)).ToList();
        if (headwordCells.Count == 0)
        {
            return [];
        }

        // Column boundary: halfway between the headword column and the most common translation start.
        var leftX = headwordCells.Min(c => c.Left);
        var rightStarts = cells.Where(c => c.Left > leftX + 40).Select(c => Math.Round(c.Left)).ToList();
        var split = rightStarts.Count == 0
            ? double.MaxValue
            : (leftX + rightStarts.GroupBy(x => x).OrderByDescending(g => g.Count()).First().Key) / 2;

        var rows = cells
            .Where(c => c.Left < split && ContainsHangul(c.Text))
            .OrderByDescending(c => c.Baseline)
            .Select(c => new RowBuilder(c))
            .ToList();

        if (rows.Count == 0)
        {
            return [];
        }

        foreach (var cell in cells.Where(c => c.Left >= split))
        {
            var nearest = rows.MinBy(r => Math.Abs(r.Headword.Baseline - cell.Baseline))!;
            if (Math.Abs(nearest.Headword.Baseline - cell.Baseline) <= AttachDistance)
            {
                nearest.Translation.Add(cell);
            }
            else if (cell.Baseline < rows[^1].Headword.Baseline - AttachDistance)
            {
                // Text below the last row (credits, notes): not part of any entry.
                continue;
            }
            else
            {
                nearest.HasStrayText = true;
                nearest.Translation.Add(cell);
            }
        }

        return rows.Select(BuildRow).ToList();
    }

    private static Row BuildRow(RowBuilder b)
    {
        var warnings = EntryWarnings.None;
        var parts = b.Translation.OrderByDescending(c => c.Baseline).ToList();
        var uzbek = string.Join(' ', parts.Select(p => p.Text)).Trim();

        if (parts.Count == 0 || uzbek.Length == 0)
        {
            warnings |= EntryWarnings.MissingTranslation;
        }
        else if (parts.Select(p => p.Line).Distinct().Count() > 1)
        {
            warnings |= EntryWarnings.MultiLineTranslation;
        }

        if (b.HasStrayText)
        {
            warnings |= EntryWarnings.UnattachedFragment;
        }

        warnings |= CheckNumbering(uzbek);

        var (korean, note) = SplitHeadword(b.Headword.Text);
        if (!IsCleanKorean(korean) || uzbek.Contains('�'))
        {
            warnings |= EntryWarnings.UnexpectedCharacters;
        }

        var rawLines = parts.Select(p => p.Line).Append(b.Headword.Line)
            .Distinct()
            .OrderByDescending(l => l.Baseline)
            .Select(l => l.Text);

        return new Row(string.Join('\n', rawLines), korean, note, uzbek, warnings);
    }

    private static EntryWarnings CheckNumbering(string text)
    {
        var numbers = SenseNumber().Matches(text).Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).ToList();
        if (numbers.Count == 0)
        {
            return EntryWarnings.None;
        }

        var result = numbers.Count > 1 ? EntryWarnings.MultipleSenses : EntryWarnings.None;
        var expected = Enumerable.Range(1, numbers.Count);
        return numbers.SequenceEqual(expected) ? result : result | EntryWarnings.BrokenNumbering;
    }

    /// <summary>
    /// "간신히 (겨우)" → ("간신히", "겨우"); "체중 = 몸무게" → ("체중", "= 몸무게"). The note keeps the printed extra
    /// (a synonym or variant) without mixing it into the headword that is practised.
    /// </summary>
    private static (string Korean, string? Note) SplitHeadword(string text)
    {
        text = text.Trim();
        var equals = text.IndexOf('=');
        var open = text.IndexOf('(');
        if (equals > 0 && (open < 0 || equals < open))
        {
            return (text[..equals].Trim(), "= " + text[(equals + 1)..].Trim());
        }

        if (open <= 0)
        {
            return (text, null);
        }

        var note = text[(open + 1)..].TrimEnd().TrimEnd(')').Trim();
        return (text[..open].Trim(), note.Length == 0 ? null : note);
    }

    /// <summary>Hangul plus the punctuation the book uses inside headwords ("…을/를", "~하다").</summary>
    private static bool IsCleanKorean(string text) =>
        text.Any(IsHangul) && text.All(ch => IsHangul(ch) || ch is ' ' or '~' or '-' or '/' or '·' or '…' or '.');

    private static bool ContainsHangul(string text) => text.Any(IsHangul);

    private static bool IsHangul(char ch) =>
        ch is >= '가' and <= '힣' or >= 'ᄀ' and <= 'ᇿ' or >= '㄰' and <= '㆏';

    private static List<Line> GroupLines(IEnumerable<LayoutWord> words)
    {
        var lines = new List<List<LayoutWord>>();
        foreach (var word in words.Where(w => !string.IsNullOrWhiteSpace(w.Text)).OrderByDescending(w => w.Baseline))
        {
            var current = lines.Count == 0 ? null : lines[^1];
            if (current is not null && Math.Abs(current[0].Baseline - word.Baseline) <= LineTolerance)
            {
                current.Add(word);
            }
            else
            {
                lines.Add([word]);
            }
        }

        return lines
            .Select(ws => new Line(ws.Average(w => w.Baseline), ws.OrderBy(w => w.Left).ToList()))
            .ToList();
    }

    private static IEnumerable<Cell> SplitCells(Line line)
    {
        var start = 0;
        for (var i = 1; i <= line.Words.Count; i++)
        {
            if (i == line.Words.Count || line.Words[i].Left - line.Words[i - 1].Right > CellGap)
            {
                var words = line.Words.Skip(start).Take(i - start).ToList();
                yield return new Cell(line.Baseline, words[0].Left, string.Join(' ', words.Select(w => w.Text)), line);
                start = i;
            }
        }
    }

    [GeneratedRegex(@"^[\p{Lu} ]+\s(\d{1,4})$")]
    private static partial Regex FooterPattern();

    [GeneratedRegex(@"(?<![\d\w])(\d{1,2})\.(?=\s|\p{L})")]
    private static partial Regex SenseNumber();
}
