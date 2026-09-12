using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.AmazonMusic.Catalog;
using Jellyfin.Plugin.AmazonMusic.Catalog.Models;
using Jellyfin.Plugin.AmazonMusic.ExternalIds;
using Jellyfin.Plugin.AmazonMusic.Organizer;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AmazonMusic.Providers;

/// <summary>Supplies artist metadata from Amazon Music.</summary>
public class ArtistMetadataProvider : IRemoteMetadataProvider<MusicArtist, ArtistInfo>
{
    private readonly IAmazonMusicCatalog _catalog;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ArtistMetadataProvider> _logger;

    /// <summary>Initializes a new instance of the <see cref="ArtistMetadataProvider"/> class.</summary>
    /// <param name="catalog">Amazon Music catalog.</param>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="logger">Logger.</param>
    public ArtistMetadataProvider(IAmazonMusicCatalog catalog, IHttpClientFactory httpClientFactory, ILogger<ArtistMetadataProvider> logger)
    {
        _catalog = catalog;
        _httpClient = httpClientFactory.CreateClient(NamedClient.Default);
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => PluginConstants.Name;

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(ArtistInfo searchInfo, CancellationToken cancellationToken)
        => (await FindAsync(searchInfo, cancellationToken)).Select(ToSearchResult);

    /// <inheritdoc />
    public async Task<MetadataResult<MusicArtist>> GetMetadata(ArtistInfo info, CancellationToken cancellationToken)
    {
        var artists = await FindAsync(info, cancellationToken);
        if (artists.Count == 0)
        {
            _logger.LogDebug("No Amazon Music artist for {Name}", info.Name);
            return new MetadataResult<MusicArtist> { HasMetadata = false };
        }

        var artist = artists[0];
        var item = new MusicArtist { Name = artist.Attributes.Name };
        item.SetProviderId(ProviderKeys.Artist, artist.Id);
        item.SetProviderId(ProviderKeys.Marketplace, artist.Marketplace);
        return new MetadataResult<MusicArtist> { Item = item, HasMetadata = true };
    }

    /// <inheritdoc />
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => _httpClient.GetAsync(new Uri(url), cancellationToken);

    private static RemoteSearchResult ToSearchResult(CatalogItem<ArtistAttributes> artist)
    {
        var result = new RemoteSearchResult
        {
            Name = artist.Attributes.Name,
            ImageUrl = artist.Attributes.Artwork?.Url,
            SearchProviderName = PluginConstants.Name,
        };
        result.SetProviderId(ProviderKeys.Artist, artist.Id);
        result.SetProviderId(ProviderKeys.Marketplace, artist.Marketplace);
        return result;
    }

    private async Task<IReadOnlyList<CatalogItem<ArtistAttributes>>> FindAsync(ArtistInfo info, CancellationToken cancellationToken)
    {
        var id = info.GetProviderId(ProviderKeys.Artist);
        if (!string.IsNullOrEmpty(id))
        {
            var artist = await _catalog.GetArtistAsync(id, info.GetProviderId(ProviderKeys.Marketplace), cancellationToken);
            return artist is null ? [] : [artist];
        }

        var tagged = FolderTag.Parse(Path.GetFileName(info.Path));
        if (tagged is not null)
        {
            var artist = await _catalog.GetArtistAsync(tagged, null, cancellationToken);
            if (artist is not null)
            {
                return [artist];
            }
        }

        return await _catalog.SearchArtistsAsync(info.Name, cancellationToken);
    }
}
