using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security;
using System.Text;

namespace Lugat.Api.Tests.Catalog;

/// <summary>A row of the official list as the file has it: 순위, 단어 (with the homonym number), 품사, 풀이, 등급.</summary>
public sealed record ListRow(int Rank, string Word, string Pos, string? Gloss, string Level);

/// <summary>Builds upload files in the layout of 국립국어원 「한국어 학습용 어휘 목록」 (spec §2.1).</summary>
public static class CatalogFiles
{
    public static readonly string[] Header = ["순위", "단어", "품사", "풀이", "등급"];

    /// <summary>
    /// A list with the real size (A 982, B 2,111, C 2,872 = 5,965 words). Ranks are global and mixed across
    /// levels, some words in every level are bound nouns (의), auxiliaries (보) or determiners (관).
    /// </summary>
    public static List<ListRow> OfficialSize()
    {
        var rows = new List<ListRow>();
        var counts = new Dictionary<string, int> { ["A"] = 982, ["B"] = 2111, ["C"] = 2872 };
        for (var i = 0; rows.Count < 5965; i++)
        {
            // Levels interleave by rank as in the real list: mostly A at the top, C at the bottom.
            var level = i % 6 < 1 && counts["A"] > 0 ? "A" : i % 6 < 3 && counts["B"] > 0 ? "B" : counts["C"] > 0 ? "C" : counts["B"] > 0 ? "B" : "A";
            counts[level]--;
            var n = rows.Count + 1;
            var m = n / 6; // every level gets some of each
            var pos = m % 10 == 0 ? "의" : m % 15 == 7 ? "보" : m % 20 == 3 ? "관" : n % 2 == 0 ? "동" : "명";
            rows.Add(new ListRow(n, $"{(char)('가' + n)}다{(n % 3 == 0 ? "01" : "")}", pos, n % 4 == 0 ? "風" : null, level));
        }

        return rows;
    }

    /// <summary>An .xlsx with one sheet: a title line, the header, then the rows (inline strings, numbers for 순위).</summary>
    public static byte[] Xlsx(IEnumerable<ListRow> rows, string[]? header = null)
    {
        var sheet = new StringBuilder("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");
        var r = 1;
        void Row(IEnumerable<object?> cells)
        {
            sheet.Append($"<row r=\"{r}\">");
            foreach (var (cell, c) in cells.Select((x, i) => (x, i)))
            {
                var at = $"{(char)('A' + c)}{r}";
                sheet.Append(cell switch
                {
                    null => "",
                    int n => $"<c r=\"{at}\"><v>{n}</v></c>",
                    var s => $"<c r=\"{at}\" t=\"inlineStr\"><is><t>{SecurityElement.Escape(s.ToString())}</t></is></c>",
                });
            }

            sheet.Append("</row>");
            r++;
        }

        Row(["한국어 학습용 어휘 목록"]);
        Row(header ?? Header);
        foreach (var row in rows)
        {
            Row([row.Rank, row.Word, row.Pos, row.Gloss, row.Level]);
        }

        sheet.Append("</sheetData></worksheet>");
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string name, string text)
            {
                using var w = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
                w.Write(text);
            }

            Add("[Content_Types].xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>""");
            Add("_rels/.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            Add("xl/workbook.xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="어휘" sheetId="1" r:id="rId1"/></sheets></workbook>""");
            Add("xl/_rels/workbook.xml.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>""");
            Add("xl/worksheets/sheet1.xml", sheet.ToString());
        }

        return buffer.ToArray();
    }

    /// <summary>The text version: tab-separated, in the Korean code page 949.</summary>
    public static byte[] Cp949Text(IEnumerable<ListRow> rows)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var text = new StringBuilder(string.Join('\t', Header)).Append("\r\n");
        foreach (var row in rows)
        {
            text.Append($"{row.Rank}\t{row.Word}\t{row.Pos}\t{row.Gloss}\t{row.Level}\r\n");
        }

        return Encoding.GetEncoding(949).GetBytes(text.ToString());
    }

    public static MultipartFormDataContent Upload(byte[] content, string fileName)
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return new MultipartFormDataContent { { file, "file", fileName } };
    }
}
