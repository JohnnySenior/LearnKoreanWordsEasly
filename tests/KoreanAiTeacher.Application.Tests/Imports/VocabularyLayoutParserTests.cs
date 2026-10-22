using KoreanAiTeacher.Application.Imports;
using KoreanAiTeacher.Domain.Imports;

namespace KoreanAiTeacher.Application.Tests.Imports;

public class VocabularyLayoutParserTests
{
    private static ParsedEntry Single(PageBuilder page, string korean) =>
        VocabularyLayoutParser.Parse([page.Rows(5, "채움").Build()]).Entries.Single(e => e.Korean == korean);

    [Fact]
    public void Reads_rows_in_printed_order_with_page_numbers()
    {
        var page = new PageBuilder(2)
            .Row("도리어", "aksincha")
            .Row("고집", "o’jarlik")
            .Row("피하다", "qochmoq, oldini olmoq")
            .Rows(3)
            .Footer(2)
            .Build();

        var result = VocabularyLayoutParser.Parse([page]);

        Assert.Equal(6, result.Entries.Count);
        var first = result.Entries[0];
        Assert.Equal(("도리어", "aksincha"), (first.Korean, first.Uzbek));
        Assert.Equal((2, 2, 1, 1, 1), (first.PdfPage, first.PrintedPage!.Value, first.TableNumber, first.OrderInTable, first.OrderInDocument));
        Assert.Equal("qochmoq, oldini olmoq", result.Entries[2].Uzbek);
        Assert.All(result.Entries.Take(3), e => Assert.Equal(EntryWarnings.None, e.Warnings));
    }

    [Fact]
    public void Wrapped_translation_is_joined_and_flagged_for_review()
    {
        var entry = Single(
            new PageBuilder(29)
                .Row("수입", "import")
                .WrappedRow("맺다", "1. (끈을 ~) bog’lamoq 2. (끝을, 말을 ~) yakunlamoq 3. (열매를 ~)", "meva bermoq, natijasini bermoq")
                .Row("삶다", "qaynatmoq"),
            "맺다");

        Assert.Equal("1. (끈을 ~) bog’lamoq 2. (끝을, 말을 ~) yakunlamoq 3. (열매를 ~) meva bermoq, natijasini bermoq", entry.Uzbek);
        Assert.True(entry.Warnings.HasFlag(EntryWarnings.MultiLineTranslation));
        Assert.True(entry.Warnings.HasFlag(EntryWarnings.MultipleSenses));
        Assert.False(entry.Warnings.HasFlag(EntryWarnings.BrokenNumbering));
    }

    [Fact]
    public void Neighbours_of_a_wrapped_row_keep_their_own_translation()
    {
        var entries = VocabularyLayoutParser.Parse([
            new PageBuilder(29)
                .Row("수입", "import")
                .WrappedRow("맺다", "birinchi qator", "ikkinchi qator")
                .Row("삶다", "qaynatmoq")
                .Rows(3)
                .Build(),
        ]).Entries;

        Assert.Equal("import", entries.Single(e => e.Korean == "수입").Uzbek);
        Assert.Equal("qaynatmoq", entries.Single(e => e.Korean == "삶다").Uzbek);
    }

    [Theory]
    [InlineData("qolmaslik, ishlatib bo’lmoq 2. teshilmoq")]   // "1." missing in the book (바닥나다)
    [InlineData("termoq 2. (병을~ ) ochmoq 3. olmoq")]
    public void Numbering_that_does_not_start_at_one_is_flagged(string uzbek)
    {
        var entry = Single(new PageBuilder(2).Row("바닥나다", uzbek), "바닥나다");

        Assert.True(entry.Warnings.HasFlag(EntryWarnings.BrokenNumbering));
        Assert.True(entry.Warnings.IsBlocking());
    }

    [Fact]
    public void Sense_number_without_a_space_still_counts()
    {
        var entry = Single(new PageBuilder(2).Row("비비다", "1.ishqalamoq 2. (ovqatni) aralashtirmoq"), "비비다");

        Assert.Equal(EntryWarnings.MultipleSenses, entry.Warnings);
        Assert.False(entry.Warnings.IsBlocking());
    }

    [Theory]
    [InlineData("간신히 (겨우)", "간신히", "겨우")]
    [InlineData("체중 = 몸무게", "체중", "= 몸무게")]
    [InlineData("매연 (=연기)", "매연", "=연기")]
    [InlineData("대책 (~쓰다/세우다)", "대책", "~쓰다/세우다")]
    public void Printed_extras_next_to_the_headword_become_a_note(string printed, string korean, string note)
    {
        var entry = Single(new PageBuilder(2).Row(printed, "tarjima"), korean);

        Assert.Equal(note, entry.KoreanNote);
        Assert.Equal(EntryWarnings.None, entry.Warnings);
    }

    [Theory]
    [InlineData("…을/를 담당하다")]
    [InlineData("…에 의하다")]
    [InlineData("밤을 새우다")]
    public void Expressions_are_not_dropped(string korean)
    {
        var entry = Single(new PageBuilder(22).Row(korean, "ma’no"), korean);

        Assert.Equal(EntryWarnings.None, entry.Warnings);
    }

    [Fact]
    public void Cover_and_credit_pages_are_skipped()
    {
        var cover = new PageBuilder(1)
            .Text("Musaev", 60, 400)
            .Text("Farhod", 110, 400)
            .Row("중급", "Urganch 2021")
            .Build();
        var words = new PageBuilder(2).Rows(6).Footer(2).Build();

        var result = VocabularyLayoutParser.Parse([cover, words]);

        Assert.Equal([1], result.SkippedPages);
        Assert.All(result.Entries, e => Assert.Equal(1, e.TableNumber));
    }

    [Fact]
    public void Tables_are_numbered_per_word_page()
    {
        var result = VocabularyLayoutParser.Parse([
            new PageBuilder(3).Rows(5, "셋").Build(),
            new PageBuilder(2).Rows(5, "둘").Build(),
        ]);

        Assert.Equal([1, 1, 1, 1, 1, 2, 2, 2, 2, 2], result.Entries.Select(e => e.TableNumber));
        Assert.Equal(Enumerable.Range(1, 10), result.Entries.Select(e => e.OrderInDocument));
        Assert.StartsWith("둘", result.Entries[0].Korean);
    }

    [Fact]
    public void Repeated_headword_is_flagged_on_the_later_row_only()
    {
        var result = VocabularyLayoutParser.Parse([
            new PageBuilder(2).Row("맡다", "olmoq (역할을 ~)").Rows(5).Build(),
            new PageBuilder(3).Row("맡다", "hid bilmoq").Rows(5, "셋").Build(),
        ]);

        var rows = result.Entries.Where(e => e.Korean == "맡다").ToList();
        Assert.False(rows[0].Warnings.HasFlag(EntryWarnings.PossibleDuplicate));
        Assert.True(rows[1].Warnings.HasFlag(EntryWarnings.PossibleDuplicate));
        Assert.Equal("hid bilmoq", rows[1].Uzbek); // both meanings are kept, never merged automatically (spec SRC04)
    }

    [Fact]
    public void Headword_without_translation_is_flagged()
    {
        var page = new PageBuilder(2).Rows(5).Text("유령", PageBuilder.HeadwordX, 300).Build();

        var entry = VocabularyLayoutParser.Parse([page]).Entries.Single(e => e.Korean == "유령");

        Assert.True(entry.Warnings.HasFlag(EntryWarnings.MissingTranslation));
    }

    [Fact]
    public void Credits_below_the_last_row_are_ignored()
    {
        var page = new PageBuilder(52)
            .Rows(5)
            .Text("Ushbu kitob tarjima qilindi.", 72, 200)
            .Text("F-TOPIK: @f_topik", PageBuilder.TranslationX, 150)
            .Build();

        var result = VocabularyLayoutParser.Parse([page]);

        Assert.Equal(5, result.Entries.Count);
        Assert.All(result.Entries, e => Assert.Equal(EntryWarnings.None, e.Warnings));
    }

    [Fact]
    public void Raw_text_keeps_every_printed_line_of_the_entry()
    {
        var entry = Single(new PageBuilder(29).WrappedRow("맺다", "birinchi", "ikkinchi"), "맺다");

        Assert.Equal("birinchi\n맺다\nikkinchi", entry.RawText);
    }
}
