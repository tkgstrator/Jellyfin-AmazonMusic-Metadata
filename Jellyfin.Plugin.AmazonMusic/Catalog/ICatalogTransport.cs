using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.AmazonMusic.Catalog;

/// <summary>
/// Carries catalog requests to whatever serves the Amazon Music API.
/// </summary>
/// <remarks>
/// Returns the raw body rather than a deserialized object so that a caching
/// decorator can store exactly what came back, with no serialization round
/// trip. Implementations differ only in base URL and how the request is
/// authorised.
/// </remarks>
public interface ICatalogTransport
{
    /// <summary>
    /// Issues a catalog request.
    /// </summary>
    /// <param name="request">Request to issue.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response body, or null when the resource was not found.</returns>
    Task<string?> SendAsync(CatalogRequest request, CancellationToken cancellationToken);
}
