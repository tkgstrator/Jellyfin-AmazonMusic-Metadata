using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AmazonMusic.Catalog.WebPlayer;

/// <summary>
/// Obtains anonymous catalog configuration from the public Amazon Music web player.
/// </summary>
public sealed class WebPlayerBootstrapProvider : IWebPlayerBootstrapProvider, IDisposable
{
    private const string UserAgent = "Mozilla/5.0 AppleWebKit/537.36 Chrome/131.0.0.0 Safari/537.36";
    private static readonly TimeSpan _lifetime = TimeSpan.FromHours(12);

    private readonly HttpClient _httpClient;
    private readonly ILogger<WebPlayerBootstrapProvider> _logger;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, WebPlayerBootstrap> _snapshots = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="WebPlayerBootstrapProvider"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client used for public web player resources.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="time">Clock; defaults to the system clock.</param>
    public WebPlayerBootstrapProvider(
        HttpClient httpClient,
        ILogger<WebPlayerBootstrapProvider> logger,
        TimeProvider? time = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<WebPlayerBootstrap> GetAsync(
        string marketplace,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        var definition = MarketplaceDefinition.Resolve(marketplace);
        if (!forceRefresh && TryGetSnapshot(definition.Id, out var snapshot))
        {
            return snapshot;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!forceRefresh && TryGetSnapshot(definition.Id, out snapshot))
            {
                return snapshot;
            }

            snapshot = await FetchAsync(definition, cancellationToken);
            _snapshots[definition.Id] = snapshot;
            return snapshot;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
        => _gate.Dispose();

    private bool TryGetSnapshot(string marketplace, out WebPlayerBootstrap snapshot)
    {
        if (_snapshots.TryGetValue(marketplace, out var existing)
            && _time.GetUtcNow() - existing.AcquiredAt < _lifetime)
        {
            snapshot = existing;
            return true;
        }

        snapshot = null!;
        return false;
    }

    private async Task<WebPlayerBootstrap> FetchAsync(
        MarketplaceDefinition marketplace,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Refreshing anonymous Amazon Music web player configuration for {Marketplace}", marketplace.Id);

        var configUrl = new Uri(marketplace.WebPlayerOrigin, "/config.json?skipToken=false");
        using var configRequest = new HttpRequestMessage(HttpMethod.Post, configUrl);
        configRequest.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        using var configResponse = await _httpClient.SendAsync(configRequest, cancellationToken);
        configResponse.EnsureSuccessStatusCode();
        var configBody = await configResponse.Content.ReadAsStringAsync(cancellationToken);
        var config = WebPlayerBootstrapParser.ParseConfig(configBody, marketplace);

        using var bundleRequest = new HttpRequestMessage(HttpMethod.Get, config.DragonflyBundle);
        bundleRequest.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        bundleRequest.Headers.TryAddWithoutValidation("Accept-Encoding", "gzip");
        using var bundleResponse = await _httpClient.SendAsync(bundleRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        bundleResponse.EnsureSuccessStatusCode();
        var bundle = await ReadBundleAsync(bundleResponse, cancellationToken);
        var anonymousApiKey = WebPlayerBootstrapParser.ExtractAnonymousApiKey(bundle);

        _logger.LogInformation(
            "Anonymous Amazon Music web player configuration acquired for {Marketplace}, version {Version}",
            marketplace.Id,
            config.AppVersion);

        return new WebPlayerBootstrap(
            marketplace,
            config.GraphQlEndpoint,
            config.DragonflyBundle,
            anonymousApiKey,
            config.AppVersion,
            config.DeviceType,
            config.DeviceId,
            config.SessionId,
            config.Csrf,
            _time.GetUtcNow());
    }

    private static async Task<string> ReadBundleAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        if (response.Content.Headers.ContentEncoding.Contains("gzip"))
        {
            await using var gzip = new GZipStream(source, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            return await reader.ReadToEndAsync(cancellationToken);
        }

        using var plain = new StreamReader(source);
        return await plain.ReadToEndAsync(cancellationToken);
    }
}
