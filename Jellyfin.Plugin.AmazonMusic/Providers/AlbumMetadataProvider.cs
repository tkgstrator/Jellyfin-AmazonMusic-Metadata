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

/// <summary>Supplies album metadata from Amazon Music.</summary>
public class AlbumMetadataProvider : IRemoteMetadataProvider<MusicAlbum, AlbumInfo>
{
    private readonly IAmazonMusicCatalog _catalog;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AlbumMetadataProvider> _logger;

    /// <summary>Initializes a new instance of the <see cref="AlbumMetadataProvider"/> class.</summary>
    /// <param name="catalog">Amazon Music catalog.</param>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="logger">Logger.</param>
    public AlbumMetadataProvider(IAmazonMusicCatalog catalog, IHttpClientFactory httpClientFactory, ILogger<AlbumMetadataProvider> logger)
    {
        _catalog = catalog;
        _httpClient = httpClientFactory.CreateClient(NamedClient.Default);
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => PluginConstants.Name;

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(AlbumInfo searchInfo, CancellationToken cancellationToken)
        => (await FindAsync(searchInfo, cancellationToken)).Select(ToSearchResult);

    /// <inheritdoc />
    public async Task<MetadataResult<MusicAlbum>> GetMetadata(AlbumInfo info, CancellationToken cancellationToken)
    {
        var albums = await FindAsync(info, cancellationToken);
        if (albums.Count == 0)
        {
            _logger.LogDebug("No Amazon Music album for {Name}", info.Name);
            return new MetadataResult<MusicAlbum> { HasMetadata = false };
        }

        var album = albums[0];
        var attributes = album.Attributes;
        var artists = attributes.Artists.Select(artist => artist.Name).ToList();
        var item = new MusicAlbum
        {
            Name = attributes.Name,
            AlbumArtists = artists,
            Artists = artists,
        };
        if (attributes.ReleaseDate is { } released)
        {
            item.PremiereDate = released.UtcDateTime;
            item.ProductionYear = released.Year;
        }

        item.SetProviderId(ProviderKeys.Album, album.Id);
        item.SetProviderId(ProviderKeys.Marketplace, album.Marketplace);
        return new MetadataResult<MusicAlbum> { Item = item, HasMetadata = true };
    }

    /// <inheritdoc />
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => _httpClient.GetAsync(new Uri(url), cancellationToken);

    internal static string BuildSearchTerm(AlbumInfo info)
    {
        var artist = info.AlbumArtists.Count > 0 ? info.AlbumArtists[0] : string.Empty;
        return string.Join(' ', new[] { artist, info.Name }.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static RemoteSearchResult ToSearchResult(CatalogItem<AlbumAttributes> album)
    {
        var result = new RemoteSearchResult
        {
            Name = album.Attributes.Name,
            ImageUrl = album.Attributes.Artwork?.Url,
            SearchProviderName = PluginConstants.Name,
            ProductionYear = album.Attributes.ReleaseDate?.Year,
        };
        result.SetProviderId(ProviderKeys.Album, album.Id);
        result.SetProviderId(ProviderKeys.Marketplace, album.Marketplace);
        return result;
    }

    private async Task<IReadOnlyList<CatalogItem<AlbumAttributes>>> FindAsync(AlbumInfo info, CancellationToken cancellationToken)
    {
        var id = info.GetProviderId(ProviderKeys.Album);
        if (!string.IsNullOrEmpty(id))
        {
            var album = await _catalog.GetAlbumAsync(id, info.GetProviderId(ProviderKeys.Marketplace), cancellationToken);
            return album is null ? [] : [album];
        }

        var tagged = FolderTag.Parse(Path.GetFileName(info.Path));
        if (tagged is not null)
        {
            var album = await _catalog.GetAlbumAsync(tagged, null, cancellationToken);
            if (album is not null)
            {
                return [album];
            }
        }

        return await _catalog.SearchAlbumsAsync(BuildSearchTerm(info), cancellationToken);
    }
}
