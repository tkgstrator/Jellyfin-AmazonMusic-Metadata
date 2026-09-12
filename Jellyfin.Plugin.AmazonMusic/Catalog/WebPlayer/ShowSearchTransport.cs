using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.AmazonMusic.Catalog.WebPlayer;

/// <summary>
/// Sends anonymous searches through the desktop Web Player backend.
/// </summary>
public sealed class ShowSearchTransport : ICatalogTransport
{
    private static readonly Uri _endpoint = new("https://fe.web.skill.music.a2z.com/api/showSearch");

    private readonly HttpClient _httpClient;
    private readonly IWebPlayerBootstrapProvider _bootstrapProvider;
    private readonly Func<TimeSpan> _timeout;
    private readonly TimeProvider _time;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowSearchTransport"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client.</param>
    /// <param name="bootstrapProvider">Anonymous Web Player bootstrap provider.</param>
    /// <param name="timeout">Supplies the current request timeout.</param>
    /// <param name="time">Clock; defaults to the system clock.</param>
    public ShowSearchTransport(
        HttpClient httpClient,
        IWebPlayerBootstrapProvider bootstrapProvider,
        Func<TimeSpan> timeout,
        TimeProvider? time = null)
    {
        _httpClient = httpClient;
        _bootstrapProvider = bootstrapProvider;
        _timeout = timeout;
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<string?> SendAsync(CatalogRequest request, CancellationToken cancellationToken)
    {
        var bootstrap = await _bootstrapProvider.GetAsync(request.Marketplace, false, cancellationToken);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_timeout());

        var keyword = ShowSearchRequestBuilder.ReadKeyword(request);
        var body = ShowSearchRequestBuilder.BuildWireBody(
            keyword,
            bootstrap,
            Guid.NewGuid().ToString(),
            _time.GetUtcNow().ToUnixTimeMilliseconds());
        using var message = new HttpRequestMessage(HttpMethod.Post, _endpoint);
        message.Content = new StringContent(body, Encoding.UTF8, "text/plain");
        message.Headers.TryAddWithoutValidation("Origin", bootstrap.Marketplace.WebPlayerOrigin.GetLeftPart(UriPartial.Authority));
        message.Headers.Referrer = new Uri(bootstrap.Marketplace.WebPlayerOrigin, "/");
        message.Headers.TryAddWithoutValidation("User-Agent", ShowSearchRequestBuilder.UserAgent);
        message.Headers.Accept.ParseAdd("*/*");

        using var response = await _httpClient.SendAsync(message, timeoutSource.Token);
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
