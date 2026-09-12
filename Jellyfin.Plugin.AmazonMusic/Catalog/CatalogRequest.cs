using System;
using System.Net.Http;

namespace Jellyfin.Plugin.AmazonMusic.Catalog;

/// <summary>
/// Describes a catalog request independently of its authentication headers.
/// </summary>
public sealed class CatalogRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogRequest"/> class.
    /// </summary>
    /// <param name="method">HTTP method.</param>
    /// <param name="relativeUrl">Path and query, starting with '/'.</param>
    /// <param name="body">Request body, or null when the request has none.</param>
    /// <param name="cacheKey">Stable logical cache key.</param>
    /// <param name="kind">Request kind used for independent throttling.</param>
    /// <param name="marketplace">Marketplace whose catalog should answer the request.</param>
    /// <param name="validateResponse">Validates a successful body before it enters the cache.</param>
    public CatalogRequest(
        HttpMethod method,
        string relativeUrl,
        string? body,
        string cacheKey,
        CatalogRequestKind kind,
        string marketplace = CatalogOptions.Japan,
        Action<string>? validateResponse = null)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativeUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplace);

        if (!relativeUrl.StartsWith('/'))
        {
            throw new ArgumentException("The catalog URL must start with '/'.", nameof(relativeUrl));
        }

        if (method == HttpMethod.Post && string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("A POST catalog request must have a body.", nameof(body));
        }

        Method = method;
        RelativeUrl = relativeUrl;
        Body = body;
        CacheKey = cacheKey;
        Kind = kind;
        Marketplace = marketplace;
        ValidateResponse = validateResponse;
    }

    /// <summary>
    /// Gets the HTTP method.
    /// </summary>
    public HttpMethod Method { get; }

    /// <summary>
    /// Gets the path and query relative to the catalog endpoint.
    /// </summary>
    public string RelativeUrl { get; }

    /// <summary>
    /// Gets the request body.
    /// </summary>
    public string? Body { get; }

    /// <summary>
    /// Gets the stable logical cache key.
    /// </summary>
    public string CacheKey { get; }

    /// <summary>
    /// Gets the request kind used for independent throttling.
    /// </summary>
    public CatalogRequestKind Kind { get; }

    /// <summary>
    /// Gets the marketplace whose catalog should answer the request.
    /// </summary>
    public string Marketplace { get; }

    /// <summary>
    /// Gets the successful-response validator.
    /// </summary>
    internal Action<string>? ValidateResponse { get; }
}
