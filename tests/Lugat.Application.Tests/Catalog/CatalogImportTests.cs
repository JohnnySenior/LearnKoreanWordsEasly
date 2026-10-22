using Lugat.Application.Catalog;
using Lugat.Domain.Catalog;

namespace Lugat.Application.Tests.Catalog;

public class CatalogImportTests
{
    private static IReadOnlyList<string?>[] Sheet(params string?[][] rows) => rows;

    [Fact]
    public void Columns_are_found_by_header_and_homonym_numbers_split_off()
    {
        var rows = CatalogImport.Parse(Sheet(
            ["한국어 학습용 어휘 목록"],
            ["등급", "단어", "순위", "품사", "풀이"],
            ["A", "가다01", "29", "동", null],
            ["B", "가격", "1203", "명", "價格"],
            [null, null, null, null, null]));

        Assert.Equal(2, rows.DataRows);
        Assert.Equal(new CatalogRow(3, 29, "가다", "01", "동", null, CatalogLevel.A), rows.Rows[0]);
        Assert.Equal(("가격", null, "價格", CatalogLevel.B), (rows.Rows[1].Word, rows.Rows[1].Homonym, rows.Rows[1].Gloss, rows.Rows[1].Level));
        Assert.Equal("A:가다01:동", rows.Rows[0].Key);
    }

    [Theory]
    [InlineData("x", "가다", "동", "A", "순위")]
    [InlineData("3", "", "동", "A", "단어")]
    [InlineData("3", "가다", "", "A", "품사")]
    [InlineData("3", "가다", "동", "1", "등급")]
    public void Bad_rows_are_skipped_with_the_reason(string rank, string word, string pos, string level, string reason)
    {
        var rows = CatalogImport.Parse(Sheet(["순위", "단어", "품사", "등급"], [rank, word, pos, level]));

        Assert.Empty(rows.Rows);
        Assert.Contains(reason, Assert.Single(rows.Problems));
    }

    [Fact]
    public void A_file_without_the_columns_is_refused() =>
        Assert.Throws<ValidationException>(() => CatalogImport.Parse(Sheet(["No", "Word"], ["1", "가다"])));

    [Theory]
    [InlineData("bormoq; ketmoq", "hada", "학교에 가요.", "Maktabga boraman.", null)]
    [InlineData("бормоқ", "hada", null, null, "Tarjima")]
    [InlineData("a; b; c; d", "hada", null, null, "1–3")]
    [InlineData("bormoq", "ha da1", null, null, "o‘qilishi")]
    [InlineData("bormoq", "hada", "가 나 다 라 마 바 사 아 자", "Gap.", "8")]
    [InlineData("bormoq", "hada", "학교에 가요.", null, "birga")]
    public void Content_rules_of_the_spec_are_checked(string translation, string romanization, string? exampleKo, string? exampleUz, string? problem)
    {
        var (fields, message) = CatalogContent.Check(new(translation, romanization, exampleKo, exampleUz, null, null));

        if (problem is null)
        {
            Assert.Equal("bormoq; ketmoq", fields!.TranslationUz);
            Assert.True(fields.IsComplete);
        }
        else
        {
            Assert.Null(fields);
            Assert.Contains(problem, message);
        }
    }
}
