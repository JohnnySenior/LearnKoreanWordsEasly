namespace Lugat.Domain.Vocabulary;

/// <summary>
/// One meaning of a Korean word or expression that a user studies, with one card per direction.
/// Interim shape until the shared catalog and units (spec §5, stages 2–3): words are grouped in numbered tables and
/// keep their place in the list (<see cref="Order"/>), most frequent first.
/// </summary>
public sealed class WordSense
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public int TableNumber { get; set; }

    public int Order { get; set; }

    public string Korean { get; set; } = "";

    public string? KoreanNote { get; set; }

    public string Uzbek { get; set; } = "";

    /// <summary>False when the word was taken out again. Cards and history are kept (spec DATA02).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Raised whenever the practised text changes, so old AI/audio artefacts are not reused (spec AU05).</summary>
    public int ContentVersion { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
