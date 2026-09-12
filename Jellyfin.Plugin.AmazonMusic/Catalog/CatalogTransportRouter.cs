using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.AmazonMusic.Catalog;

/// <summary>
/// Routes search and lookup requests to their respective network services.
/// </summary>
public sealed class CatalogTransportRouter : ICatalogTransport
{
    private readonly ICatalogTransport _search;
    private readonly ICatalogTransport _lookup;

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogTransportRouter"/> class.
    /// </summary>
    /// <param name="search">Web Player search transport.</param>
    /// <param name="lookup">GraphQL lookup transport.</param>
    public CatalogTransportRouter(ICatalogTransport search, ICatalogTransport lookup)
    {
        _search = search;
        _lookup = lookup;
    }

    /// <inheritdoc />
    public Task<string?> SendAsync(CatalogRequest request, CancellationToken cancellationToken)
        => request.Kind == CatalogRequestKind.Search
            ? _search.SendAsync(request, cancellationToken)
            : _lookup.SendAsync(request, cancellationToken);
}
