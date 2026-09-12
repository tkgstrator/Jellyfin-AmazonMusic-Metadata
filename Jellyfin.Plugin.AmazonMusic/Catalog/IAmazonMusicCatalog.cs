using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.AmazonMusic.Catalog.Models;

namespace Jellyfin.Plugin.AmazonMusic.Catalog;

/// <summary>
/// Reads the Amazon Music catalog in configured marketplace order.
/// </summary>
public interface IAmazonMusicCatalog
{
    /// <summary>Searches for songs.</summary>
    /// <param name="term">Search term.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching songs.</returns>
    Task<IReadOnlyList<CatalogItem<SongAttributes>>> SearchSongsAsync(string term, CancellationToken cancellationToken);

    /// <summary>Searches for albums.</summary>
    /// <param name="term">Search term.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching albums.</returns>
    Task<IReadOnlyList<CatalogItem<AlbumAttributes>>> SearchAlbumsAsync(string term, CancellationToken cancellationToken);

    /// <summary>Searches for artists.</summary>
    /// <param name="term">Search term.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching artists.</returns>
    Task<IReadOnlyList<CatalogItem<ArtistAttributes>>> SearchArtistsAsync(string term, CancellationToken cancellationToken);

    /// <summary>Looks a song up by identifier.</summary>
    /// <param name="id">Catalog identifier.</param>
    /// <param name="marketplace">Owning marketplace, or null to try configured marketplaces.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The song, or null.</returns>
    Task<CatalogItem<SongAttributes>?> GetSongAsync(string id, string? marketplace, CancellationToken cancellationToken);

    /// <summary>Looks an album up by identifier.</summary>
    /// <param name="id">Catalog identifier.</param>
    /// <param name="marketplace">Owning marketplace, or null to try configured marketplaces.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The album, or null.</returns>
    Task<CatalogItem<AlbumAttributes>?> GetAlbumAsync(string id, string? marketplace, CancellationToken cancellationToken);

    /// <summary>Looks an artist up by identifier.</summary>
    /// <param name="id">Catalog identifier.</param>
    /// <param name="marketplace">Owning marketplace, or null to try configured marketplaces.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The artist, or null.</returns>
    Task<CatalogItem<ArtistAttributes>?> GetArtistAsync(string id, string? marketplace, CancellationToken cancellationToken);
}
