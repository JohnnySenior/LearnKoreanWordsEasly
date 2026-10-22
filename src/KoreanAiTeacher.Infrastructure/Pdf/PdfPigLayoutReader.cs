using KoreanAiTeacher.Application.Abstractions;
using KoreanAiTeacher.Application.Imports;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Exceptions;

namespace KoreanAiTeacher.Infrastructure.Pdf;

public sealed class PdfPigLayoutReader : IPdfLayoutReader
{
    public int CountPages(Stream pdf)
    {
        using var doc = Open(pdf);
        return doc.NumberOfPages;
    }

    public IReadOnlyList<LayoutPage> ReadPages(Stream pdf)
    {
        using var doc = Open(pdf);
        return doc.GetPages()
            .Select(p => new LayoutPage(
                p.Number,
                p.GetWords()
                    .Select(w => new LayoutWord(w.Text, w.BoundingBox.Left, w.BoundingBox.Right, w.BoundingBox.Bottom))
                    .ToList()))
            .ToList();
    }

    public byte[] ExtractPages(Stream pdf, int fromPage, int toPage)
    {
        using var doc = Open(pdf);
        if (fromPage < 1 || toPage > doc.NumberOfPages || fromPage > toPage)
        {
            throw new PdfReadException($"Kitobda {doc.NumberOfPages} bet bor. {fromPage}–{toPage}-betlarni olib bo‘lmaydi.");
        }

        var builder = new UglyToad.PdfPig.Writer.PdfDocumentBuilder();
        for (var page = fromPage; page <= toPage; page++)
        {
            builder.AddPage(doc, page);
        }

        return builder.Build();
    }

    private static PdfDocument Open(Stream pdf)
    {
        try
        {
            return PdfDocument.Open(pdf);
        }
        catch (PdfDocumentEncryptedException ex)
        {
            throw new PdfReadException("PDF parol bilan himoyalangan. Parolsiz nusxasini yuklang.", ex);
        }
        catch (Exception ex) when (ex is PdfDocumentFormatException or InvalidOperationException or ArgumentException)
        {
            throw new PdfReadException("PDF faylni o‘qib bo‘lmadi. Fayl buzilgan bo‘lishi mumkin.", ex);
        }
    }
}
