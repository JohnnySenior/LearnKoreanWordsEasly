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
    public void A_word_without_a_rank_is_kept()
    {
        // The official list has 68 such words, mostly proper nouns (고).
        var rows = CatalogImport.Parse(Sheet(["순위", "단어", "품사", "풀이", "등급"], ["", "서울", "고", null, "A"]));

        Assert.Equal(("서울", null, "고"), (rows.Rows.Single().Word, rows.Rows[0].Rank, rows.Rows[0].Pos));
        Assert.Empty(rows.Problems);
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

    private static CatalogRows ReadText(string text, System.Text.Encoding encoding, bool bom = false)
    {
        var bytes = (bom ? encoding.GetPreamble() : []).Concat(encoding.GetBytes(text)).ToArray();
        return CatalogImport.Parse(new Lugat.Infrastructure.Catalog.CatalogFileReader().ReadRows(new MemoryStream(bytes), "list.txt"));
    }

    [Fact]
    public void A_space_separated_text_file_keeps_glosses_with_spaces_and_lines_without_a_gloss()
    {
        var rows = ReadText("순위 단어 품사 풀이 등급\n1 것01 의 A\n2 하다01 동 A\n29 가다01 동 길 따위를 A\n", System.Text.Encoding.UTF8);

        Assert.Equal(3, rows.Rows.Count);
        Assert.Empty(rows.Problems);
        Assert.Equal(("것", "01", "의", null, CatalogLevel.A), (rows.Rows[0].Word, rows.Rows[0].Homonym, rows.Rows[0].Pos, rows.Rows[0].Gloss, rows.Rows[0].Level));
        Assert.Equal(("가다", "길 따위를", CatalogLevel.A), (rows.Rows[2].Word, rows.Rows[2].Gloss, rows.Rows[2].Level));
    }

    [Fact]
    public void Unicode_text_as_Excel_saves_it_is_read()
    {
        var rows = ReadText("순위\t단어\t품사\t풀이\t등급\r\n2\t하다01\t동\t\tA\r\n", System.Text.Encoding.Unicode, bom: true);

        Assert.Equal(("하다", (int?)2, CatalogLevel.A), (rows.Rows.Single().Word, rows.Rows[0].Rank, rows.Rows[0].Level));
    }

    [Fact]
    public void Korean_code_page_text_is_read()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var rows = ReadText("순위\t단어\t품사\t풀이\t등급\n2\t하다01\t동\t\tA\n", System.Text.Encoding.GetEncoding(949));

        Assert.Equal("하다", rows.Rows.Single().Word);
    }

    [Fact]
    public void A_file_without_a_header_is_read_in_the_official_order()
    {
        var rows = ReadText("1 것01 의 A\n2 하다01 동 A\n4 있다01 형 A\n5 되다01 동 B\n", System.Text.Encoding.UTF8);

        Assert.Equal(["것", "하다", "있다", "되다"], rows.Rows.Select(r => r.Word));
        Assert.Equal(CatalogLevel.B, rows.Rows[3].Level);
    }
}
