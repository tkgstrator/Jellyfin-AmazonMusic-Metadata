using System;
using System.Collections.Generic;
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

/// <summary>Supplies track metadata from Amazon Music.</summary>
public class SongMetadataProvider : IRemoteMetadataProvider<Audio, SongInfo>
{
    private readonly IAmazonMusicCatalog _catalog;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SongMetadataProvider> _logger;

    /// <summary>Initializes a new instance of the <see cref="SongMetadataProvider"/> class.</summary>
    /// <param name="catalog">Amazon Music catalog.</param>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="logger">Logger.</param>
    public SongMetadataProvider(IAmazonMusicCatalog catalog, IHttpClientFactory httpClientFactory, ILogger<SongMetadataProvider> logger)
    {
        _catalog = catalog;
        _httpClient = httpClientFactory.CreateClient(NamedClient.Default);
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => PluginConstants.Name;

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SongInfo searchInfo, CancellationToken cancellationToken)
        => (await FindAsync(searchInfo, cancellationToken)).Select(ToSearchResult);

    /// <inheritdoc />
    public async Task<MetadataResult<Audio>> GetMetadata(SongInfo info, CancellationToken cancellationToken)
    {
        var songs = await FindAsync(info, cancellationToken);
        if (songs.Count == 0)
        {
            _logger.LogDebug("No Amazon Music song for {Name}", info.Name);
            return new MetadataResult<Audio> { HasMetadata = false };
        }

        var song = songs[0];
        var attributes = song.Attributes;
        var artists = attributes.Artists.Select(artist => artist.Name).ToArray();
        var item = new Audio
        {
            Name = attributes.Name,
            Album = attributes.AlbumName,
            Artists = artists,
            AlbumArtists = artists,
            IndexNumber = attributes.TrackNumber,
            ParentIndexNumber = attributes.DiscNumber,
        };
        if (attributes.ReleaseDate is { } released)
        {
            item.PremiereDate = released.UtcDateTime;
            item.ProductionYear = released.Year;
        }

        item.SetProviderId(ProviderKeys.Song, song.Id);
        item.SetProviderId(ProviderKeys.Marketplace, song.Marketplace);
        var albumId = song.AlbumIds.Count > 0 ? song.AlbumIds[0] : attributes.AlbumId ?? FolderTag.FindInAncestors(info.Path);
        if (albumId is not null)
        {
            item.SetProviderId(ProviderKeys.Album, albumId);
        }

        return new MetadataResult<Audio> { Item = item, HasMetadata = true };
    }

    /// <inheritdoc />
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => _httpClient.GetAsync(new Uri(url), cancellationToken);

    internal static CatalogItem<SongAttributes>? MatchTrack(IReadOnlyList<CatalogItem<SongAttributes>> tracks, SongInfo info)
    {
        var (fileDisc, fileTrack, fileTitle) = FileNames.ParseTrackFileName(info.Path ?? string.Empty);
        var trackNumber = info.IndexNumber is > 0 ? info.IndexNumber : fileTrack;
        var discNumber = info.IndexNumber is > 0 ? info.ParentIndexNumber : fileDisc;
        if (trackNumber is > 0)
        {
            var byNumber = tracks.Where(track => track.Attributes.TrackNumber == trackNumber
                && (discNumber is null || track.Attributes.DiscNumber is null || track.Attributes.DiscNumber == discNumber)).ToList();
            if (byNumber.Count == 1)
            {
                return byNumber[0];
            }
        }

        foreach (var title in new[] { info.Name, fileTitle })
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var byName = tracks.Where(track => string.Equals(FileNames.Sanitize(track.Attributes.Name), FileNames.Sanitize(title), StringComparison.OrdinalIgnoreCase)).ToList();
            if (byName.Count == 1)
            {
                return byName[0];
            }
        }

        return null;
    }

    internal static string BuildSearchTerm(SongInfo info)
    {
        var artist = info.AlbumArtists.Count > 0 ? info.AlbumArtists[0] : info.Artists.Count > 0 ? info.Artists[0] : null;
        return string.Join(' ', new[] { artist, info.Album, info.Name }.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static RemoteSearchResult ToSearchResult(CatalogItem<SongAttributes> song)
    {
        var result = new RemoteSearchResult
        {
            Name = song.Attributes.Name,
            ImageUrl = song.Attributes.Artwork?.Url,
            SearchProviderName = PluginConstants.Name,
            ProductionYear = song.Attributes.ReleaseDate?.Year,
        };
        result.SetProviderId(ProviderKeys.Song, song.Id);
        result.SetProviderId(ProviderKeys.Marketplace, song.Marketplace);
        return result;
    }

    private async Task<IReadOnlyList<CatalogItem<SongAttributes>>> FindAsync(SongInfo info, CancellationToken cancellationToken)
    {
        var id = info.GetProviderId(ProviderKeys.Song);
        if (!string.IsNullOrEmpty(id))
        {
            var song = await _catalog.GetSongAsync(id, info.GetProviderId(ProviderKeys.Marketplace), cancellationToken);
            return song is null ? [] : [song];
        }

        var albumId = info.GetProviderId(ProviderKeys.Album) ?? FolderTag.FindInAncestors(info.Path);
        if (albumId is not null)
        {
            var album = await _catalog.GetAlbumAsync(albumId, info.GetProviderId(ProviderKeys.Marketplace), cancellationToken);
            var track = album is null ? null : MatchTrack(album.Tracks, info);
            if (track is not null)
            {
                return [track];
            }
        }

        return await _catalog.SearchSongsAsync(BuildSearchTerm(info), cancellationToken);
    }
}
