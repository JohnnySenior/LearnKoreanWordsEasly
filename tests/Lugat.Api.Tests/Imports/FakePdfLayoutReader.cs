using Lugat.Application.Abstractions;
using Lugat.Application.Imports;

namespace Lugat.Api.Tests.Imports;

/// <summary>
/// Stands in for PdfPig so HTTP tests control what the "PDF" contains. Parsing of real PDFs is covered in
/// Lugat.Application.Tests.
/// </summary>
internal sealed class FakePdfLayoutReader : IPdfLayoutReader
{
    public List<LayoutPage> Pages { get; } = [];

    public PdfReadException? FailOnCount { get; set; }

    public PdfReadException? FailOnRead { get; set; }

    public int CountPages(Stream pdf) => FailOnCount is { } ex ? throw ex : Pages.Count;

    public IReadOnlyList<LayoutPage> ReadPages(Stream pdf) => FailOnRead is { } ex ? throw ex : Pages;

    public List<(int From, int To)> Extracted { get; } = [];

    public byte[] ExtractPages(Stream pdf, int fromPage, int toPage)
    {
        Extracted.Add((fromPage, toPage));
        return System.Text.Encoding.ASCII.GetBytes($"%PDF-1.7 page {fromPage}");
    }

    /// <summary>A word page laid out like the real book (headword x=78, translation x=223, 21pt rows).</summary>
    public FakePdfLayoutReader AddPage(params (string Korean, string Uzbek)[] rows)
    {
        var words = new List<LayoutWord>();
        var y = 692.0;
        foreach (var (korean, uzbek) in rows)
        {
            words.Add(new LayoutWord(korean, 78, 78 + (korean.Length * 11), y));
            var x = 223.0;
            foreach (var w in uzbek.Split(' '))
            {
                words.Add(new LayoutWord(w, x, x + (w.Length * 5), y + 1));
                x += (w.Length * 5) + 3;
            }

            y -= 21;
        }

        Pages.Add(new LayoutPage(Pages.Count + 1, words));
        return this;
    }

    public static (string, string)[] Words(string prefix, int count) =>
        Enumerable.Range(0, count).Select(i => ($"{prefix}{(char)('가' + i)}", $"ma’no {i + 1}")).ToArray();
}
