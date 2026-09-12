using System.Collections.Generic;

namespace Jellyfin.Plugin.AmazonMusic.Catalog;

/// <summary>
/// A catalog resource together with the marketplace it was found in.
/// </summary>
/// <typeparam name="TAttributes">Attribute payload type.</typeparam>
public class CatalogItem<TAttributes>
    where TAttributes : class
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogItem{TAttributes}"/> class.
    /// </summary>
    /// <param name="id">Amazon Music catalog identifier.</param>
    /// <param name="marketplace">Marketplace the resource was found in.</param>
    /// <param name="attributes">Attribute payload.</param>
    public CatalogItem(string id, string marketplace, TAttributes attributes)
    {
        Id = id;
        Marketplace = marketplace;
        Attributes = attributes;
    }

    /// <summary>
    /// Gets the Amazon Music catalog identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the marketplace the resource was found in. Catalog identifiers are
    /// marketplace-scoped, so this must be kept alongside the id.
    /// </summary>
    public string Marketplace { get; }

    /// <summary>
    /// Gets the attribute payload.
    /// </summary>
    public TAttributes Attributes { get; }

    /// <summary>
    /// Gets the ids of the related artists. Populated by id lookups only.
    /// </summary>
    public IReadOnlyList<string> ArtistIds { get; init; } = [];

    /// <summary>
    /// Gets the ids of the related albums. Populated by song id lookups only.
    /// </summary>
    public IReadOnlyList<string> AlbumIds { get; init; } = [];

    /// <summary>
    /// Gets the album's tracks, in order. Populated by album id lookups only.
    /// </summary>
    public IReadOnlyList<CatalogItem<Models.SongAttributes>> Tracks { get; init; } = [];
}
