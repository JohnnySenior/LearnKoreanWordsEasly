namespace Lugat.Domain.Vocabulary;

/// <summary>
/// One meaning of a Korean word or expression that a user studies (spec §12 Lexeme/WordSense).
/// Created from an approved source row; the row's place in the book is kept for lesson planning.
/// Different printed rows are never merged automatically (spec SRC04).
/// </summary>
public sealed class WordSense
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public Guid SourceEntryId { get; set; }

    public Guid SourceDocumentId { get; set; }

    public int TableNumber { get; set; }

    public int OrderInDocument { get; set; }

    public string Korean { get; set; } = "";

    public string? KoreanNote { get; set; }

    public string Uzbek { get; set; } = "";

    /// <summary>False when the source row was excluded again. Cards and history are kept (spec DATA02).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Raised whenever the practised text changes, so old AI/audio artefacts are not reused (spec AU05).</summary>
    public int ContentVersion { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
