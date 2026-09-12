using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.AmazonMusic.Catalog.GraphQl;
using Jellyfin.Plugin.AmazonMusic.Catalog.Models;
using Jellyfin.Plugin.AmazonMusic.Catalog.Parsing;
using Jellyfin.Plugin.AmazonMusic.Catalog.WebPlayer;

namespace Jellyfin.Plugin.AmazonMusic.Catalog;

/// <summary>
/// Reads search templates and GraphQL metadata from Amazon Music.
/// </summary>
public sealed class AmazonMusicCatalog : IAmazonMusicCatalog
{
    private readonly ICatalogTransport _searchTransport;
    private readonly ICatalogTransport _lookupTransport;
    private readonly Func<CatalogOptions> _options;

    /// <summary>Initializes a new instance of the <see cref="AmazonMusicCatalog"/> class.</summary>
    /// <param name="searchTransport">Transport for Web Player searches.</param>
    /// <param name="lookupTransport">Transport for GraphQL lookups.</param>
    /// <param name="options">Current catalog options.</param>
    public AmazonMusicCatalog(ICatalogTransport searchTransport, ICatalogTransport lookupTransport, Func<CatalogOptions> options)
    {
        _searchTransport = searchTransport;
        _lookupTransport = lookupTransport;
        _options = options;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CatalogItem<SongAttributes>>> SearchSongsAsync(string term, CancellationToken cancellationToken)
        => SearchAsync(term, CatalogResponseParser.ParseSongs, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<CatalogItem<AlbumAttributes>>> SearchAlbumsAsync(string term, CancellationToken cancellationToken)
        => SearchAsync(term, CatalogResponseParser.ParseAlbums, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<CatalogItem<ArtistAttributes>>> SearchArtistsAsync(string term, CancellationToken cancellationToken)
        => SearchAsync(term, CatalogResponseParser.ParseArtists, cancellationToken);

    /// <inheritdoc />
    public Task<CatalogItem<SongAttributes>?> GetSongAsync(string id, string? marketplace, CancellationToken cancellationToken)
        => LookupAsync(id, marketplace, "trackMetadata", AmazonMusicOperations.TrackMetadata, "id", CatalogResponseParser.ParseSong, cancellationToken);

    /// <inheritdoc />
    public async Task<CatalogItem<AlbumAttributes>?> GetAlbumAsync(string id, string? marketplace, CancellationToken cancellationToken)
    {
        foreach (var current in Marketplaces(marketplace))
        {
            var metadata = await SendLookupAsync(current, "getAlbumMetadata", AmazonMusicOperations.GetAlbumMetadata, "id", id, cancellationToken);
            if (metadata is null)
            {
                continue;
            }

            var tracks = await SendLookupAsync(current, "getAlbumTracks", AmazonMusicOperations.GetAlbumTracks, "id", id, cancellationToken);
            if (tracks is null)
            {
                throw new CatalogProtocolException("Amazon Music returned album metadata without its track list.");
            }

            return CatalogResponseParser.ParseAlbum(metadata, tracks, current);
        }

        return null;
    }

    /// <inheritdoc />
    public Task<CatalogItem<ArtistAttributes>?> GetArtistAsync(string id, string? marketplace, CancellationToken cancellationToken)
        => LookupAsync(id, marketplace, "getArtistSummary", AmazonMusicOperations.GetArtistSummary, "artistId", CatalogResponseParser.ParseArtist, cancellationToken);

    private async Task<IReadOnlyList<CatalogItem<T>>> SearchAsync<T>(
        string term,
        Func<string, string, IReadOnlyList<CatalogItem<T>>> parse,
        CancellationToken cancellationToken)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return [];
        }

        foreach (var marketplace in _options().Marketplaces)
        {
            var body = await _searchTransport.SendAsync(ShowSearchRequestBuilder.Build(marketplace, term), cancellationToken);
            if (body is null)
            {
                continue;
            }

            var results = parse(body, marketplace);
            if (results.Count > 0)
            {
                return results;
            }
        }

        return [];
    }

    private async Task<CatalogItem<T>?> LookupAsync<T>(
        string id,
        string? marketplace,
        string operation,
        string query,
        string variableName,
        Func<string, string, CatalogItem<T>?> parse,
        CancellationToken cancellationToken)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        foreach (var current in Marketplaces(marketplace))
        {
            var body = await SendLookupAsync(current, operation, query, variableName, id, cancellationToken);
            if (body is null)
            {
                continue;
            }

            var item = parse(body, current);
            if (item is not null)
            {
                return item;
            }
        }

        return null;
    }

    private async Task<string?> SendLookupAsync(
        string marketplace,
        string operation,
        string query,
        string variableName,
        string id,
        CancellationToken cancellationToken)
    {
        var variables = variableName == "artistId" ? new { artistId = id } : (object)new { id };
        var rootName = operation == "trackMetadata" ? "track" : operation == "getArtistSummary" ? "artist" : "album";
        var request = GraphQlRequestBuilder.Build(marketplace, operation, query, variables, CatalogRequestKind.Lookup, rootName);
        return await _lookupTransport.SendAsync(request, cancellationToken);
    }

    private IReadOnlyList<string> Marketplaces(string? marketplace)
        => string.IsNullOrWhiteSpace(marketplace) ? _options().Marketplaces : [marketplace];
}
