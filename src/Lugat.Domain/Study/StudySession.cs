using Lugat.Domain.Learning;

namespace Lugat.Domain.Study;

public enum StudySessionStatus
{
    Active,
    Completed,
    Abandoned,
}

/// <summary>One planned prompt in a session.</summary>
public sealed record SessionItem(Guid CardId, Guid WordSenseId, ReviewDirection Direction, SessionItemKind Kind);

/// <summary>Why the planner picked the card (spec PLAN08). Shown in results, and useful for tests.</summary>
public enum SessionItemKind
{
    Relearning,

    /// <summary>A due card of the words in focus: the table being drilled.</summary>
    FocusReview,
    Review,
    Overdue,
    New,
}

/// <summary>
/// A practice session with its planned queue, so it can be resumed exactly where it stopped (spec UX07, §12 Session).
/// </summary>
public sealed class StudySession
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    /// <summary>Chosen length; null means "cheklovsiz" (spec PLAN06).</summary>
    public int? Minutes { get; set; }

    public List<SessionItem> Items { get; set; } = [];

    /// <summary>Index of the next item to show.</summary>
    public int Position { get; set; }

    public StudySessionStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public uint Version { get; set; }
}
