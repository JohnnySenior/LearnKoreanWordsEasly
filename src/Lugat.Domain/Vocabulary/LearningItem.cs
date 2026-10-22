namespace Lugat.Domain.Vocabulary;

public enum LearningItemKind
{
    /// <summary>A word of the shared catalog (spec §2): one item for all learners.</summary>
    Catalog,

    /// <summary>A learner's own word (camera, v1.1+, spec §3.2). Not created in v1.</summary>
    Personal,
}

/// <summary>
/// What a card practises (spec §5.2): one Korean word with its Uzbek meaning. Cards, answers and sessions point here,
/// so one review engine works for catalog words and, later, personal ones. A catalog item is shared: its text is the
/// published content of the <see cref="CatalogWordId"/> word, kept in step with it (<see cref="ContentVersion"/>).
/// "The learner's words" are the items they have cards for.
/// </summary>
public sealed class LearningItem
{
    public Guid Id { get; set; }

    public LearningItemKind Kind { get; set; }

    /// <summary>Set for <see cref="LearningItemKind.Catalog"/>; one item per catalog word.</summary>
    public Guid? CatalogWordId { get; set; }

    /// <summary>Set only for <see cref="LearningItemKind.Personal"/>; catalog items have no owner.</summary>
    public Guid? OwnerId { get; set; }

    /// <summary>The catalog unit the word belongs to; published units never change (spec §2.2).</summary>
    public Guid? UnitId { get; set; }

    /// <summary>Place in the course, most frequent first: level, unit number, place in the unit.</summary>
    public int Order { get; set; }

    public string Korean { get; set; } = "";

    /// <summary>Tells apart words written the same in one unit (spec §2.2), e.g. the part of speech.</summary>
    public string? KoreanNote { get; set; }

    public string Uzbek { get; set; } = "";

    /// <summary>False when the word was taken out again. Cards and history are kept (spec DATA02).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Raised whenever the practised text changes, so old AI/audio artefacts are not reused (spec AU05).</summary>
    public int ContentVersion { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>The course order of a catalog word: A-1 #1 comes before A-2 #1, which comes before B-1 #1.</summary>
    public static int CourseOrder(Catalog.CatalogLevel level, int unitNumber, int orderInUnit) =>
        ((int)level * 1_000_000) + (unitNumber * 1_000) + orderInUnit;
}
