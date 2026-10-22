using Lugat.Application.Imports;
using Lugat.Domain.Imports;
using Lugat.Infrastructure.Pdf;

namespace Lugat.Application.Tests.Imports;

/// <summary>
/// Runs the full PDF → entries path on the real book. The book is personal material and is not in the repo,
/// so the test only runs when LUGAT_DICT_PDF points at a local copy.
/// </summary>
public class RealDictionaryPdfTests
{
    private static VocabularyParseResult ParseRealBook()
    {
        var path = Environment.GetEnvironmentVariable("LUGAT_DICT_PDF");
        Assert.SkipWhen(string.IsNullOrEmpty(path), "LUGAT_DICT_PDF is not set; the dictionary PDF is not part of the repository.");
        using var stream = File.OpenRead(path!);
        return VocabularyLayoutParser.Parse(new PdfPigLayoutReader().ReadPages(stream));
    }

    [Fact]
    public void Whole_book_yields_1500_words_in_51_tables()
    {
        var result = ParseRealBook();

        Assert.Equal(1500, result.Entries.Count); // the cover says 1500
        Assert.Equal(51, result.Entries.Max(e => e.TableNumber));
        Assert.Equal([1, 53], result.SkippedPages);
    }

    [Fact]
    public void First_two_tables_are_the_first_60_words()
    {
        var result = ParseRealBook();
        var firstLesson = result.Entries.Where(e => e.TableNumber <= 2).ToList();

        Assert.Equal(60, firstLesson.Count);
        Assert.Equal(("도리어", "aksincha"), (firstLesson[0].Korean, firstLesson[0].Uzbek));
        Assert.Equal(("회의장", "majlislar zali"), (firstLesson[29].Korean, firstLesson[29].Uzbek));
        Assert.Equal(("과속", "tezlikni oshirish"), (firstLesson[59].Korean, firstLesson[59].Uzbek));
        Assert.DoesNotContain(firstLesson, e => e.Warnings.IsBlocking());
    }

    [Fact]
    public void Known_tricky_rows_are_flagged()
    {
        var entries = ParseRealBook().Entries;

        Assert.True(entries.Single(e => e.Korean == "맺다").Warnings.HasFlag(EntryWarnings.MultiLineTranslation));
        Assert.True(entries.Single(e => e.Korean == "바닥나다").Warnings.HasFlag(EntryWarnings.BrokenNumbering));
        Assert.Equal(2, entries.Count(e => e.Korean == "맡다"));
        Assert.Equal("겨우", entries.Single(e => e.Korean == "간신히").KoreanNote);
        Assert.Contains(entries, e => e.Korean == "…을/를 담당하다");
    }
}
