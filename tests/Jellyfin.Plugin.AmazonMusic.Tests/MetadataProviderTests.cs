using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.AmazonMusic.Catalog;
using Jellyfin.Plugin.AmazonMusic.Catalog.Models;
using Jellyfin.Plugin.AmazonMusic.ExternalIds;
using Jellyfin.Plugin.AmazonMusic.Providers;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.AmazonMusic.Tests;

public class MetadataProviderTests
{
    [Fact]
    public async Task AlbumProviderPrefersStoredIdAndPreservesMarketplace()
    {
        var catalog = new FakeCatalog { Albums = [new("B1", "jp", new AlbumAttributes { Name = "Album", Artists = [new("A1", "Artist")] })] };
        var provider = new AlbumMetadataProvider(catalog, new StubHttpClientFactory(), NullLogger<AlbumMetadataProvider>.Instance);
        var info = new AlbumInfo { Name = "Album" };
        info.SetProviderId(ProviderKeys.Album, "B1");
        info.SetProviderId(ProviderKeys.Marketplace, "jp");

        var result = await provider.GetMetadata(info, TestContext.Current.CancellationToken);

        Assert.True(result.HasMetadata);
        Assert.Equal([("B1", "jp")], catalog.AlbumLookups);
        Assert.Empty(catalog.Searches);
        Assert.Equal("jp", result.Item.GetProviderId(ProviderKeys.Marketplace));
    }

    [Fact]
    public async Task SongProviderUsesTaggedAlbumAndFileNumber()
    {
        var catalog = new FakeCatalog
        {
            Albums = [new("B1", "jp", new AlbumAttributes { Name = "Album" })
            {
                Tracks = [new("T1", "jp", new SongAttributes { Name = "One", AlbumId = "B1", TrackNumber = 1 }), new("T2", "jp", new SongAttributes { Name = "Two", AlbumId = "B1", TrackNumber = 2 })],
            }],
        };
        var provider = new SongMetadataProvider(catalog, new StubHttpClientFactory(), NullLogger<SongMetadataProvider>.Instance);

        var result = await provider.GetMetadata(new SongInfo { Name = "02", Path = "/music/Album-[amzn-B1]/02.flac" }, TestContext.Current.CancellationToken);

        Assert.True(result.HasMetadata);
        Assert.Equal("T2", result.Item.GetProviderId(ProviderKeys.Song));
        Assert.Equal("B1", result.Item.GetProviderId(ProviderKeys.Album));
        Assert.Empty(catalog.Searches);
    }

    [Fact]
    public async Task ArtistProviderMapsIdAndMarketplace()
    {
        var catalog = new FakeCatalog { Artists = [new("A1", "jp", new ArtistAttributes { Name = "Artist" })] };
        var provider = new ArtistMetadataProvider(catalog, new StubHttpClientFactory(), NullLogger<ArtistMetadataProvider>.Instance);

        var result = await provider.GetMetadata(new ArtistInfo { Name = "Artist" }, TestContext.Current.CancellationToken);

        Assert.True(result.HasMetadata);
        Assert.Equal("A1", result.Item.GetProviderId(ProviderKeys.Artist));
        Assert.Equal("jp", result.Item.GetProviderId(ProviderKeys.Marketplace));
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private sealed class FakeCatalog : IAmazonMusicCatalog
    {
        public IReadOnlyList<CatalogItem<SongAttributes>> Songs { get; init; } = [];
        public IReadOnlyList<CatalogItem<AlbumAttributes>> Albums { get; init; } = [];
        public IReadOnlyList<CatalogItem<ArtistAttributes>> Artists { get; init; } = [];
        public List<string> Searches { get; } = [];
        public List<(string Id, string? Marketplace)> AlbumLookups { get; } = [];

        public Task<IReadOnlyList<CatalogItem<SongAttributes>>> SearchSongsAsync(string term, CancellationToken cancellationToken) { Searches.Add(term); return Task.FromResult(Songs); }
        public Task<IReadOnlyList<CatalogItem<AlbumAttributes>>> SearchAlbumsAsync(string term, CancellationToken cancellationToken) { Searches.Add(term); return Task.FromResult(Albums); }
        public Task<IReadOnlyList<CatalogItem<ArtistAttributes>>> SearchArtistsAsync(string term, CancellationToken cancellationToken) { Searches.Add(term); return Task.FromResult(Artists); }
        public Task<CatalogItem<SongAttributes>?> GetSongAsync(string id, string? marketplace, CancellationToken cancellationToken) => Task.FromResult<CatalogItem<SongAttributes>?>(Find(Songs, id));
        public Task<CatalogItem<AlbumAttributes>?> GetAlbumAsync(string id, string? marketplace, CancellationToken cancellationToken) { AlbumLookups.Add((id, marketplace)); return Task.FromResult<CatalogItem<AlbumAttributes>?>(Find(Albums, id)); }
        public Task<CatalogItem<ArtistAttributes>?> GetArtistAsync(string id, string? marketplace, CancellationToken cancellationToken) => Task.FromResult<CatalogItem<ArtistAttributes>?>(Find(Artists, id));
        private static CatalogItem<T>? Find<T>(IReadOnlyList<CatalogItem<T>> items, string id) where T : class => items.Count > 0 && items[0].Id == id ? items[0] : items.Count > 0 ? items[0] : null;
    }
}
