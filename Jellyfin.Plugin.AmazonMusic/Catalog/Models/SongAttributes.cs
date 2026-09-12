using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.AmazonMusic.Catalog.Models;

/// <summary>
/// Provider-facing song metadata.
/// </summary>
public sealed class SongAttributes
{
    /// <summary>Gets or initializes the title.</summary>
    public required string Name { get; init; }

    /// <summary>Gets or initializes the contributing artists.</summary>
    public IReadOnlyList<CatalogArtist> Artists { get; init; } = [];

    /// <summary>Gets or initializes the album identifier.</summary>
    public string? AlbumId { get; init; }

    /// <summary>Gets or initializes the album name.</summary>
    public string? AlbumName { get; init; }

    /// <summary>Gets or initializes the track number.</summary>
    public int? TrackNumber { get; init; }

    /// <summary>Gets or initializes the disc number.</summary>
    public int? DiscNumber { get; init; }

    /// <summary>Gets or initializes the release date.</summary>
    public DateTimeOffset? ReleaseDate { get; init; }

    /// <summary>Gets or initializes the artwork.</summary>
    public Artwork? Artwork { get; init; }
}
