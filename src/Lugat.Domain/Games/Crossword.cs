namespace Lugat.Domain.Games;

/// <summary>About 8, 12 or 16 words; the bigger ones need more open units to fill a grid (L-55).</summary>
public enum CrosswordSize { Small, Medium, Large }

public enum CrosswordStatus { Active, Finished, Abandoned }

public enum CrosswordWordState
{
    Open,

    /// <summary>Written by the learner (with or without hints), or completed by the words crossing it.</summary>
    Found,

    /// <summary>Shown by "So‘zni ochish": goes to the mistakes notebook like a forgotten card.</summary>
    Revealed,
}

/// <summary>
/// One word in the grid. <see cref="Shown"/> are syllable positions opened by a hint; <see cref="SawExample"/> is the
/// other hint. Hearing the word is free, as on the cards. The Korean never reaches the client while the word is open.
/// </summary>
public sealed record CrosswordWord(
    Guid LearningItemId,
    string Korean,
    string Uzbek,
    int Row,
    int Col,
    bool Down,
    int Number,
    string? ExampleKo = null,
    string? ExampleUz = null,
    CrosswordWordState State = CrosswordWordState.Open,
    List<int>? Shown = null,
    bool SawExample = false,
    DateTimeOffset? AnsweredAt = null)
{
    public string[] Syllables() => Korean.EnumerateRunes().Select(r => r.ToString()).ToArray();

    public int Hints() => (Shown?.Count ?? 0) + (SawExample ? 1 : 0);

    public (int Row, int Col) Cell(int index) => Down ? (Row + index, Col) : (Row, Col + index);
}

/// <summary>
/// A crossword made from the words of the learner's open units (L-55). One is active at a time and is kept on the
/// server, so it continues on any device. It is a game: it never changes card scheduling.
/// </summary>
public sealed class Crossword
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public CrosswordSize Size { get; set; }

    public CrosswordStatus Status { get; set; }

    public int Rows { get; set; }

    public int Cols { get; set; }

    public List<CrosswordWord> Words { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public uint Version { get; set; }
}
