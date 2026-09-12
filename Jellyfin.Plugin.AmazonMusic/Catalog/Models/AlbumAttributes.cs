using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.AmazonMusic.Catalog.Models;

/// <summary>
/// Provider-facing album metadata.
/// </summary>
public sealed class AlbumAttributes
{
    /// <summary>Gets or initializes the title.</summary>
    public required string Name { get; init; }

    /// <summary>Gets or initializes the contributing artists.</summary>
    public IReadOnlyList<CatalogArtist> Artists { get; init; } = [];

    /// <summary>Gets or initializes the release date.</summary>
    public DateTimeOffset? ReleaseDate { get; init; }

    /// <summary>Gets or initializes the copyright statement.</summary>
    public string? Copyright { get; init; }

    /// <summary>Gets or initializes the number of tracks.</summary>
    public int? TrackCount { get; init; }

    /// <summary>Gets or initializes the artwork.</summary>
    public Artwork? Artwork { get; init; }
}
