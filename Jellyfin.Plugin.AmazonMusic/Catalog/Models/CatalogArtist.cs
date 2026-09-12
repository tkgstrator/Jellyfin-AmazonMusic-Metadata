namespace Jellyfin.Plugin.AmazonMusic.Catalog.Models;

/// <summary>
/// Identifies a contributing artist.
/// </summary>
/// <param name="Id">Amazon Music artist identifier.</param>
/// <param name="Name">Artist name.</param>
/// <param name="Role">Contribution role, when available.</param>
public sealed record CatalogArtist(string? Id, string Name, string? Role = null);
