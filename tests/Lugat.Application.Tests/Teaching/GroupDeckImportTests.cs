using Lugat.Application.Teaching;

namespace Lugat.Application.Tests.Teaching;

/// <summary>L-67: a teacher's two-column list, however the columns are laid out.</summary>
public class GroupDeckImportTests
{
    private static GroupDeckRows Parse(params string?[][] rows) => GroupDeckImport.Parse(rows);

    [Fact]
    public void Two_columns_in_either_order_with_a_header_and_extra_columns()
    {
        var result = Parse(
            ["Koreyscha", "O‘zbekcha"],
            ["학교", "maktab"],
            ["suv", "물", "(ot)"],
            ["3", "회사원", "kompaniya xodimi"],
            [null, "   ", null],
            ["사랑", "sevgi; muhabbat"]);

        Assert.Equal([("학교", "maktab"), ("물", "suv"), ("회사원", "kompaniya xodimi"), ("사랑", "sevgi; muhabbat")], result.Rows.Select(r => (r.Korean, r.Uzbek)));
        Assert.Equal(4, result.DataRows); // the header and the empty line are not rows
        Assert.Empty(result.Problems);
    }

    [Fact]
    public void One_cell_with_a_separator_or_a_number_is_split_and_cleaned()
    {
        var result = Parse(
            ["1. 학교 - maktab"],
            ["2) 물: suv"],
            ["사과 — olma"],
            ["회사원\tkompaniya xodimi"],
            ["학생 - 선생님"]); // two Korean sides: not a translation

        Assert.Equal([("학교", "maktab"), ("물", "suv"), ("사과", "olma"), ("회사원", "kompaniya xodimi")], result.Rows.Select(r => (r.Korean, r.Uzbek)));
        Assert.Single(result.Problems);
        Assert.Contains("5-qator", result.Problems[0]);
        Assert.Contains("tarjima yo‘q", result.Problems[0]);
    }

    [Fact]
    public void Rows_without_korean_a_cyrillic_translation_and_a_repeated_word_are_reported_and_skipped()
    {
        var result = Parse(
            ["학교", "maktab"],
            ["faqat o‘zbekcha"],
            ["물", "сув"],
            ["학교", "o‘quv yurti"],
            ["사랑", "sevgi"]);

        Assert.Equal(["학교", "사랑"], result.Rows.Select(r => r.Korean));
        Assert.Equal(5, result.DataRows);
        Assert.Equal(3, result.Problems.Count);
        Assert.Contains(result.Problems, p => p.StartsWith("2-qator") && p.Contains("koreyscha so‘z topilmadi"));
        Assert.Contains(result.Problems, p => p.StartsWith("3-qator") && p.Contains("lotin"));
        Assert.Contains(result.Problems, p => p.StartsWith("4-qator") && p.Contains("takrorlangan"));
    }

    [Fact]
    public void The_translation_keeps_the_catalogs_one_apostrophe_form()
    {
        var result = Parse(["학교", "o'quv yurti"]);
        Assert.Equal("o‘quv yurti", result.Rows.Single().Uzbek);
    }
}
