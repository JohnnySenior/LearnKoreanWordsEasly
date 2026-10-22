namespace Lugat.Domain.Catalog;

public sealed record UnitPlan<T>(IReadOnlyList<(int Number, IReadOnlyList<T> Words)> Units, IReadOnlyList<T> Unplaced);

/// <summary>
/// Splits a level's words into units (spec §2.2): in the given (frequency) order, 25 words each. A last unit with
/// fewer than 10 words is joined to the one before it; when there is no new unit before it (all earlier units are
/// published and cannot change) those words wait for more words of the level instead.
/// </summary>
public static class UnitSplitter
{
    public const int UnitSize = 25;

    public const int MinUnitSize = 10;

    /// <param name="ordered">Words not in a published unit, most frequent first.</param>
    /// <param name="firstNumber">The number after the last published unit of the level.</param>
    public static UnitPlan<T> Plan<T>(IReadOnlyList<T> ordered, int firstNumber)
    {
        var chunks = ordered.Chunk(UnitSize).Select(c => c.ToList()).ToList();
        var unplaced = new List<T>();
        if (chunks.Count > 0 && chunks[^1].Count < MinUnitSize)
        {
            var last = chunks[^1];
            chunks.RemoveAt(chunks.Count - 1);
            if (chunks.Count > 0)
            {
                chunks[^1].AddRange(last);
            }
            else
            {
                unplaced.AddRange(last);
            }
        }

        return new UnitPlan<T>(chunks.Select((c, i) => (firstNumber + i, (IReadOnlyList<T>)c)).ToList(), unplaced);
    }
}
