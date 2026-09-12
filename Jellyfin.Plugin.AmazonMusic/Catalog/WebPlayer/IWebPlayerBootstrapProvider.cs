using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.AmazonMusic.Catalog.WebPlayer;

/// <summary>
/// Discovers the anonymous Amazon Music web player configuration.
/// </summary>
public interface IWebPlayerBootstrapProvider
{
    /// <summary>
    /// Gets a bootstrap snapshot for a marketplace.
    /// </summary>
    /// <param name="marketplace">Plugin marketplace identifier.</param>
    /// <param name="forceRefresh">Whether to discard a cached snapshot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current bootstrap snapshot.</returns>
    Task<WebPlayerBootstrap> GetAsync(string marketplace, bool forceRefresh, CancellationToken cancellationToken);

    /// <summary>
    /// Refreshes a snapshot only when it is still current.
    /// </summary>
    /// <param name="marketplace">Plugin marketplace identifier.</param>
    /// <param name="staleSnapshot">Snapshot used by the refused request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The refreshed or already-updated snapshot.</returns>
    Task<WebPlayerBootstrap> RefreshAsync(
        string marketplace,
        WebPlayerBootstrap staleSnapshot,
        CancellationToken cancellationToken)
        => GetAsync(marketplace, true, cancellationToken);
}
