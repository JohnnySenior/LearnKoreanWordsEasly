namespace Lugat.Domain.Catalog;

public enum UnitProgressStatus
{
    /// <summary>The learner may study the unit. Its cards are created when it is first opened (spec §5.3).</summary>
    Open,

    /// <summary>The unit test was passed (stage 4); the next unit is open.</summary>
    Passed,
}

public enum UnitPassedBy
{
    Test,

    /// <summary>"Bilaman — testni topshirish" (spec §4.5).</summary>
    SkipTest,
}

/// <summary>
/// One learner's state in one unit (spec §5.1). No row means locked. Only the server changes it
/// (<c>UnitProgressService</c>): the client can never mark a unit open or passed (spec §5.3).
/// </summary>
public sealed class UserUnitProgress
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid UnitId { get; set; }

    public UnitProgressStatus Status { get; set; }

    /// <summary>When the unit became available to the learner.</summary>
    public DateTimeOffset OpenedAt { get; set; }

    /// <summary>When the learner first started it and its cards were created; null until then.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? PassedAt { get; set; }

    /// <summary>
    /// The first practice round with the unit's words (L-42): a writing round (10 of its words, or all of them) or a
    /// finished "Qaysi so‘z to‘g‘ri?" test. Only marks the "Mashq" step; the test stays open without it.
    /// </summary>
    public DateTimeOffset? PracticedAt { get; set; }

    public UnitPassedBy? PassedBy { get; set; }

    public int? BestScorePercent { get; set; }

    public int Attempts { get; set; }

    public uint Version { get; set; }
}
