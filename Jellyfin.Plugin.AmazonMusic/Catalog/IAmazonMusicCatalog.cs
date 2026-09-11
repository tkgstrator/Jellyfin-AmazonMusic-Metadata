using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.AmazonMusic.Catalog;

/// <summary>
/// Reads the Amazon Music catalog, walking the configured marketplaces until a
/// result is found.
/// </summary>
/// <typeparam name="TSong">Song attribute payload.</typeparam>
/// <typeparam name="TAlbum">Album attribute payload.</typeparam>
/// <typeparam name="TArtist">Artist attribute payload.</typeparam>
/// <remarks>
/// The payload types are left open because the response schema is not settled
/// yet. Fixing them is the first step once the transport exists; the shape of
/// the calls themselves is already decided by how the providers resolve items:
/// a stored id first, then a search as the last resort.
/// </remarks>
public interface IAmazonMusicCatalog<TSong, TAlbum, TArtist>
    where TSong : class
    where TAlbum : class
    where TArtist : class
{
    /// <summary>
    /// Searches for songs.
    /// </summary>
    /// <param name="term">Search term.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching songs; empty when nothing was found.</returns>
    Task<IReadOnlyList<CatalogItem<TSong>>> SearchSongsAsync(string term, CancellationToken cancellationToken);

    /// <summary>
    /// Searches for albums.
    /// </summary>
    /// <param name="term">Search term.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching albums; empty when nothing was found.</returns>
    Task<IReadOnlyList<CatalogItem<TAlbum>>> SearchAlbumsAsync(string term, CancellationToken cancellationToken);

    /// <summary>
    /// Searches for artists.
    /// </summary>
    /// <param name="term">Search term.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching artists; empty when nothing was found.</returns>
    Task<IReadOnlyList<CatalogItem<TArtist>>> SearchArtistsAsync(string term, CancellationToken cancellationToken);

    /// <summary>
    /// Looks a song up by catalog identifier.
    /// </summary>
    /// <param name="id">Amazon Music catalog identifier.</param>
    /// <param name="marketplace">Marketplace the id belongs to; null to try the configured ones in order.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The song, or null when it was not found.</returns>
    Task<CatalogItem<TSong>?> GetSongAsync(string id, string? marketplace, CancellationToken cancellationToken);

    /// <summary>
    /// Looks an album up by catalog identifier.
    /// </summary>
    /// <param name="id">Amazon Music catalog identifier.</param>
    /// <param name="marketplace">Marketplace the id belongs to; null to try the configured ones in order.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The album, or null when it was not found.</returns>
    Task<CatalogItem<TAlbum>?> GetAlbumAsync(string id, string? marketplace, CancellationToken cancellationToken);

    /// <summary>
    /// Looks an artist up by catalog identifier.
    /// </summary>
    /// <param name="id">Amazon Music catalog identifier.</param>
    /// <param name="marketplace">Marketplace the id belongs to; null to try the configured ones in order.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The artist, or null when it was not found.</returns>
    Task<CatalogItem<TArtist>?> GetArtistAsync(string id, string? marketplace, CancellationToken cancellationToken);
}
