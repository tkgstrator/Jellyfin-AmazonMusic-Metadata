using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.AmazonMusic.Catalog.WebPlayer;

namespace Jellyfin.Plugin.AmazonMusic.Catalog.GraphQl;

/// <summary>
/// Sends catalog requests through the anonymous Amazon Music web player GraphQL endpoint.
/// </summary>
public sealed class WebPlayerGraphQlTransport : ICatalogTransport
{
    private readonly HttpClient _httpClient;
    private readonly IWebPlayerBootstrapProvider _bootstrapProvider;
    private readonly WebPlayerIdentity _identity;
    private readonly Func<TimeSpan> _timeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebPlayerGraphQlTransport"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client.</param>
    /// <param name="bootstrapProvider">Anonymous web player bootstrap provider.</param>
    /// <param name="identity">Process-scoped anonymous identity.</param>
    /// <param name="timeout">Supplies the current request timeout.</param>
    public WebPlayerGraphQlTransport(
        HttpClient httpClient,
        IWebPlayerBootstrapProvider bootstrapProvider,
        WebPlayerIdentity identity,
        Func<TimeSpan> timeout)
    {
        _httpClient = httpClient;
        _bootstrapProvider = bootstrapProvider;
        _identity = identity;
        _timeout = timeout;
    }

    /// <inheritdoc />
    public async Task<string?> SendAsync(CatalogRequest request, CancellationToken cancellationToken)
    {
        var bootstrap = await _bootstrapProvider.GetAsync(request.Marketplace, false, cancellationToken);
        var response = await SendOnceAsync(request, bootstrap, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            response.Dispose();
            bootstrap = await _bootstrapProvider.RefreshAsync(request.Marketplace, bootstrap, cancellationToken);
            response = await SendOnceAsync(request, bootstrap, cancellationToken);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if ((int)response.StatusCode == 429)
            {
                throw new CatalogRateLimitedException();
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
    }

    private async Task<HttpResponseMessage> SendOnceAsync(
        CatalogRequest request,
        WebPlayerBootstrap bootstrap,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_timeout());

        using var message = new HttpRequestMessage(request.Method, new Uri(bootstrap.GraphQlEndpoint, request.RelativeUrl));
        if (request.Body is not null)
        {
            message.Content = new StringContent(request.Body, Encoding.UTF8, "application/json");
        }

        message.Headers.TryAddWithoutValidation("x-api-key", bootstrap.AnonymousApiKey);
        message.Headers.TryAddWithoutValidation("x-amzn-device-id", _identity.DeviceId);
        message.Headers.TryAddWithoutValidation("x-amzn-device-type", bootstrap.DeviceType);
        message.Headers.TryAddWithoutValidation("x-amzn-session-id", _identity.SessionId);
        message.Headers.TryAddWithoutValidation("music-territory", bootstrap.Marketplace.Territory);
        message.Headers.TryAddWithoutValidation("Accept-Language", bootstrap.Marketplace.Locale);
        message.Headers.TryAddWithoutValidation("x-amzn-client-app-version", bootstrap.AppVersion);
        message.Headers.TryAddWithoutValidation("x-amzn-trace-start", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
        message.Headers.TryAddWithoutValidation("Origin", bootstrap.Marketplace.WebPlayerOrigin.GetLeftPart(UriPartial.Authority));
        message.Headers.Referrer = bootstrap.Marketplace.WebPlayerOrigin;
        message.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 AppleWebKit/537.36 Chrome/131.0.0.0 Safari/537.36");
        message.Headers.Accept.ParseAdd("application/json");

        return await _httpClient.SendAsync(message, timeoutSource.Token);
    }
}
