using Lugat.Application.Imports;

namespace Lugat.Application.Abstractions;

/// <summary>Private storage for original files (spec SRC01). Keys are opaque; callers never build paths.</summary>
public interface IFileStore
{
    Task SaveAsync(string key, Stream content, CancellationToken ct);

    Task<Stream?> OpenReadAsync(string key, CancellationToken ct);

    Task DeleteAsync(string key, CancellationToken ct);
}

public sealed class PdfReadException(string userMessage, Exception? inner = null) : Exception(userMessage, inner)
{
    /// <summary>Uzbek text safe to show to the user.</summary>
    public string UserMessage { get; } = userMessage;
}

/// <summary>Extracts positioned words from a PDF (spec IMP03: text layer first, no AI).</summary>
public interface IPdfLayoutReader
{
    /// <exception cref="PdfReadException">Encrypted, damaged or unreadable file.</exception>
    int CountPages(Stream pdf);

    /// <exception cref="PdfReadException">Encrypted, damaged or unreadable file.</exception>
    IReadOnlyList<LayoutPage> ReadPages(Stream pdf);

    /// <summary>A new, small PDF with only the given pages (1-based, inclusive), to send one page to the AI.</summary>
    /// <exception cref="PdfReadException">Encrypted, damaged or unreadable file.</exception>
    byte[] ExtractPages(Stream pdf, int fromPage, int toPage);
}
