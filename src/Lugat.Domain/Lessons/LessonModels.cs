using Lugat.Domain.Learning;

namespace Lugat.Domain.Lessons;

public enum AssignmentStatus
{
    Planned,

    /// <summary>Handed in to the teacher. Its words keep being reviewed (spec PLAN05).</summary>
    Done,
}

/// <summary>A lesson the teacher will test: which vocabulary tables, due when (spec PLAN01, PLAN02, §12 LessonAssignment).</summary>
public sealed class LessonAssignment
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public Guid SourceDocumentId { get; set; }

    public string Title { get; set; } = "";

    public int[] Tables { get; set; } = [];

    public DateTimeOffset DueAt { get; set; }

    public AssignmentStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

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
    LessonReview,
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

    public Guid? AssignmentId { get; set; }

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
