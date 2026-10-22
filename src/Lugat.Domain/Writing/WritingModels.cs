using Lugat.Domain.Learning;

namespace Lugat.Domain.Writing;

/// <summary>How a typed answer was judged (spec PR02, PR06).</summary>
public enum WritingVerdict
{
    Correct,

    /// <summary>The right word with a small spelling slip (a jamo or a letter). Counted apart from meaning errors.</summary>
    Spelling,

    /// <summary>Another word with the same meaning: not wrong, but not the word this card practises (PR06).</summary>
    SameMeaning,

    Wrong,
}

/// <summary>
/// One typed answer in the writing exercise. Kept apart from flash-card reviews: it never changes a card's schedule,
/// and its results are shown separately (spec SRS06, D-23).
/// </summary>
public sealed class WritingAttempt
{
    /// <summary>Chosen by the client and reused on retries (spec API01).</summary>
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public Guid WordSenseId { get; set; }

    /// <summary>KoreanToUzbek = the learner types Uzbek; UzbekToKorean = the learner types Korean.</summary>
    public ReviewDirection Direction { get; set; }

    public string Answer { get; set; } = "";

    public string Expected { get; set; } = "";

    public WritingVerdict Verdict { get; set; }

    /// <summary>Set when the AI judged the meaning because the text did not match (PR06).</summary>
    public bool AiChecked { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
