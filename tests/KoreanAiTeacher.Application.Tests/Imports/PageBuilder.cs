using KoreanAiTeacher.Application.Imports;

namespace KoreanAiTeacher.Application.Tests.Imports;

/// <summary>Builds pages with the coordinates measured in the real F-TOPIK PDF: headwords at x=78, translations at x=223, rows 21pt apart.</summary>
internal sealed class PageBuilder(int pdfPage)
{
    public const double HeadwordX = 78;
    public const double TranslationX = 223;

    private readonly List<LayoutWord> _words = [];
    private double _y = 692;

    public PageBuilder Row(string korean, string uzbek)
    {
        Cell(korean, HeadwordX, _y);
        Cell(uzbek, TranslationX, _y + 1);
        _y -= 21;
        return this;
    }

    /// <summary>A translation printed on two lines, one above and one below the headword (as in the book).</summary>
    public PageBuilder WrappedRow(string korean, string firstLine, string secondLine)
    {
        Cell(firstLine, TranslationX, _y + 7);
        Cell(korean, HeadwordX, _y);
        Cell(secondLine, TranslationX, _y - 9);
        _y -= 27;
        return this;
    }

    public PageBuilder Rows(int count, string prefix = "단어")
    {
        for (var i = 0; i < count; i++)
        {
            Row($"{prefix}{(char)('가' + i)}", $"tarjima {i + 1}");
        }

        return this;
    }

    public PageBuilder Footer(int printed)
    {
        Cell($"FARKHOD MUSAEV {printed}", 425, 58);
        return this;
    }

    public PageBuilder Text(string text, double x, double y)
    {
        Cell(text, x, y);
        return this;
    }

    public LayoutPage Build() => new(pdfPage, _words.ToList());

    /// <summary>Splits on spaces into words ~5pt wide per char with 3pt gaps, like PdfPig's word extraction.</summary>
    private void Cell(string text, double x, double y)
    {
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var width = word.Length * 5;
            _words.Add(new LayoutWord(word, x, x + width, y));
            x += width + 3;
        }
    }
}
