namespace Lugat.Domain.Teaching;

public enum GroupDeckStatus
{
    /// <summary>The teacher is still editing it; learners do not see it.</summary>
    Draft,

    /// <summary>Given to the group: every member has its cards.</summary>
    Published,

    /// <summary>Taken back: the cards are gone from the learners, the deck stays in the teacher's history.</summary>
    Archived,
}

/// <summary>Where a group word's card content (reading, example, trap) came from.</summary>
public enum GroupWordSource
{
    /// <summary>Only the teacher's two columns so far.</summary>
    Teacher,

    /// <summary>The checked catalog had the word: its content is used.</summary>
    Catalog,

    /// <summary>The AI wrote the reading, example and trap; the translation stays the teacher's.</summary>
    Ai,
}

/// <summary>
/// A teacher's own vocabulary list for one group (L-67): the words of a lesson from the book the teacher uses, in
/// Korean with the teacher's Uzbek translation. Published, it becomes cards for every member of the group; it never
/// enters the course catalog or the public word pages.
/// </summary>
public sealed class GroupDeck
{
    public Guid Id { get; set; }

    public Guid GroupId { get; set; }

    public Guid TeacherUserId { get; set; }

    public string Name { get; set; } = "";

    public GroupDeckStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public DateTimeOffset? ArchivedAt { get; set; }

    /// <summary>
    /// The day the homework is due (L-71), as the teacher chose it; <see cref="DueAt"/> is the end of that day in the
    /// teacher's time zone. Both null: no deadline.
    /// </summary>
    public DateOnly? DueOn { get; set; }

    public DateTimeOffset? DueAt { get; set; }

    public uint Version { get; set; }

    public const int MaxName = 60;

    public const int MaxWords = 200;
}

/// <summary>
/// One word of a <see cref="GroupDeck"/>. The translation is the teacher's and is never rewritten. When the checked
/// catalog has the word with that meaning, <see cref="SharedItemId"/> points at the course's learning item, so a
/// learner keeps one memory of the word; otherwise each learner gets a personal copy with the teacher's text.
/// </summary>
public sealed class GroupDeckWord
{
    public Guid Id { get; set; }

    public Guid GroupDeckId { get; set; }

    public int Order { get; set; }

    public string Korean { get; set; } = "";

    public string Uzbek { get; set; } = "";

    public GroupWordSource Source { get; set; }

    /// <summary>The catalog word the content was taken from, whether or not the meaning matched.</summary>
    public Guid? CatalogWordId { get; set; }

    /// <summary>The course's learning item, when the teacher's translation is one of the catalog word's meanings.</summary>
    public Guid? SharedItemId { get; set; }

    public string? Romanization { get; set; }

    public string? ExampleKo { get; set; }

    public string? ExampleUz { get; set; }

    public string? TrapUz { get; set; }

    public string? TrapCue { get; set; }

    public string? TrapWrong { get; set; }

    public string? TrapRight { get; set; }

    public const int MaxKorean = 50;

    public const int MaxUzbek = 300;
}
