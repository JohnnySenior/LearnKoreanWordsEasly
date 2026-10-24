namespace Lugat.Domain.Users;

/// <summary>
/// Visits that came with an ad link (<c>/?src=ig</c>, L-40), counted per channel and Korea day: the top of the funnel,
/// before anyone signs in. Nothing about the visitor is stored.
/// </summary>
public sealed class SourceVisit
{
    public string Source { get; set; } = "";

    public DateOnly Day { get; set; }

    public int Count { get; set; }
}
