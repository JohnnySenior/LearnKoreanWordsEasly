namespace Lugat.Domain.Teaching;

/// <summary>
/// A teacher's group (spec §9.1). Learners join through <c>/join/{InviteCode}</c> and agree that the teacher sees
/// their unit results and most-missed words.
/// </summary>
public sealed class Group
{
    public Guid Id { get; set; }

    public Guid TeacherUserId { get; set; }

    public string Name { get; set; } = "";

    /// <summary>Random, unguessable, unique; the teacher may make a new one (the old link then stops working).</summary>
    public string InviteCode { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The teacher closed the group: members are no longer shown and the link stops working.</summary>
    public DateTimeOffset? ClosedAt { get; set; }

    public uint Version { get; set; }
}

/// <summary>
/// A learner in a group (spec §5.1). One row per group and learner: leaving sets <see cref="LeftAt"/>, joining again
/// clears it. The teacher sees only members with no <see cref="LeftAt"/> (spec §13.8).
/// </summary>
public sealed class GroupMember
{
    public Guid Id { get; set; }

    public Guid GroupId { get; set; }

    public Guid UserId { get; set; }

    public DateTimeOffset JoinedAt { get; set; }

    public DateTimeOffset? LeftAt { get; set; }
}

/// <summary>
/// The owner paid a teacher's commission for one month (spec §9.3). Paying happens outside the app; the admin only
/// marks it, so the teacher's report shows the month as paid.
/// </summary>
public sealed class CommissionPayout
{
    public Guid Id { get; set; }

    public Guid TeacherUserId { get; set; }

    /// <summary>The first day of the month (Korea time) the commission was earned in.</summary>
    public DateOnly Month { get; set; }

    /// <summary>The commission due when it was marked paid.</summary>
    public int AmountKrw { get; set; }

    public DateTimeOffset PaidAt { get; set; }

    public Guid MarkedByUserId { get; set; }
}
