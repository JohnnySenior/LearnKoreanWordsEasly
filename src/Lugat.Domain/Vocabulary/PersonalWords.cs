namespace Lugat.Domain.Vocabulary;

/// <summary>Where a learner's own word came from (L-51): the checked catalog first, the AI only when it has none.</summary>
public enum PersonalWordSource
{
    Catalog,
    Ai,
}

/// <summary>A named group of a learner's own words ("Ish", "Bozor"). Words with no deck are in "Umumiy".</summary>
public sealed class WordDeck
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public string Name { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }

    public const int MaxName = 40;
}

/// <summary>
/// A word the learner added to "Mening so‘zlarim" (L-51). It points at the item the cards practise: the shared catalog
/// item when the word is in a published unit (no copy, no duplicate cards later), else a personal item with the text
/// and the card notes kept here.
/// </summary>
public sealed class PersonalWord
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public Guid LearningItemId { get; set; }

    public Guid? DeckId { get; set; }

    /// <summary>Set when the word came from a teacher's group deck (L-67): the learner practises it but cannot remove it.</summary>
    public Guid? GroupDeckId { get; set; }

    public Guid? GroupDeckWordId { get; set; }

    /// <summary>The deck made this row (not a word the learner had before and the deck linked): leaving the group takes it away.</summary>
    public bool FromGroupDeck { get; set; }

    public PersonalWordSource Source { get; set; }

    /// <summary>The catalog word the content was taken from, when it was.</summary>
    public Guid? CatalogWordId { get; set; }

    public string? Romanization { get; set; }

    public string? ExampleKo { get; set; }

    public string? ExampleUz { get; set; }

    public string? TrapUz { get; set; }

    /// <summary>The trap acted out on the card (L-52), as on a catalog word.</summary>
    public string? TrapCue { get; set; }

    public string? TrapWrong { get; set; }

    public string? TrapRight { get; set; }

    /// <summary>
    /// The day its new cards may join the daily review: at most <see cref="NewPerDay"/> own words a day, so adding fifty
    /// at once does not bury the next days.
    /// </summary>
    public DateOnly IntroduceOn { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public const int NewPerDay = 10;

    /// <summary>The first day from <paramref name="today"/> with room left, given how many words each day already has.</summary>
    public static DateOnly NextFreeDay(DateOnly today, IReadOnlyDictionary<DateOnly, int> perDay)
    {
        var day = today;
        while (perDay.GetValueOrDefault(day) >= NewPerDay)
        {
            day = day.AddDays(1);
        }

        return day;
    }
}
