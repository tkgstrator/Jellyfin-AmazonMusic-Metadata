namespace Jellyfin.Plugin.AmazonMusic.Catalog.Models;

/// <summary>
/// Provider-facing artist metadata.
/// </summary>
public sealed class ArtistAttributes
{
    /// <summary>Gets or initializes the artist name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets or initializes the artwork.</summary>
    public Artwork? Artwork { get; init; }
}
