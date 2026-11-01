namespace Lugat.Domain.Catalog;

/// <summary>
/// The public address of a catalog word, <c>/soz/{Slug}</c> (L-56). Given once, when the word is first public, and
/// never changed, so links and search results keep working. A separate row: giving it never touches the word's own
/// row (or its xmin, which an editor may be holding).
/// </summary>
public sealed class WordSlug
{
    public Guid CatalogWordId { get; set; }

    public string Slug { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }
}
