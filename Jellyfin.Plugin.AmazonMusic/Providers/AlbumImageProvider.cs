using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.AmazonMusic.Catalog;
using Jellyfin.Plugin.AmazonMusic.ExternalIds;
using Jellyfin.Plugin.AmazonMusic.Organizer;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.AmazonMusic.Providers;

/// <summary>Supplies album artwork from Amazon Music.</summary>
public class AlbumImageProvider : IRemoteImageProvider
{
    private readonly IAmazonMusicCatalog _catalog;
    private readonly HttpClient _httpClient;

    /// <summary>Initializes a new instance of the <see cref="AlbumImageProvider"/> class.</summary>
    /// <param name="catalog">Amazon Music catalog.</param>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    public AlbumImageProvider(IAmazonMusicCatalog catalog, IHttpClientFactory httpClientFactory)
    {
        _catalog = catalog;
        _httpClient = httpClientFactory.CreateClient(NamedClient.Default);
    }

    /// <inheritdoc />
    public string Name => PluginConstants.Name;

    /// <inheritdoc />
    public bool Supports(BaseItem item) => item is MusicAlbum;

    /// <inheritdoc />
    public IEnumerable<ImageType> GetSupportedImages(BaseItem item) => [ImageType.Primary];

    /// <inheritdoc />
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => _httpClient.GetAsync(new Uri(url), cancellationToken);

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        if (item is not MusicAlbum album)
        {
            return [];
        }

        var id = album.GetProviderId(ProviderKeys.Album) ?? FolderTag.Parse(Path.GetFileName(album.Path));
        var candidates = string.IsNullOrEmpty(id)
            ? await _catalog.SearchAlbumsAsync(SearchTermFor(album), cancellationToken)
            : await LookupAsync(id, album.GetProviderId(ProviderKeys.Marketplace), cancellationToken);
        return candidates.Where(candidate => candidate.Attributes.Artwork is not null).Select(candidate => ImageInfo(candidate.Attributes.Artwork!)).ToList();
    }

    private async Task<IReadOnlyList<CatalogItem<Catalog.Models.AlbumAttributes>>> LookupAsync(string id, string? marketplace, CancellationToken cancellationToken)
    {
        var album = await _catalog.GetAlbumAsync(id, marketplace, cancellationToken);
        return album is null ? [] : [album];
    }

    private static string SearchTermFor(MusicAlbum album)
    {
        var artist = album.AlbumArtists.Count > 0 ? album.AlbumArtists[0] : string.Empty;
        return string.Join(' ', new[] { artist, album.Name }.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static RemoteImageInfo ImageInfo(Catalog.Models.Artwork artwork) => new()
    {
        ProviderName = PluginConstants.Name,
        Type = ImageType.Primary,
        Url = artwork.Url,
        Width = artwork.Width,
        Height = artwork.Height,
        ThumbnailUrl = artwork.Url,
    };
}
