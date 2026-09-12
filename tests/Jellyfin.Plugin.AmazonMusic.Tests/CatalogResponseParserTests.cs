using System;
using System.IO;
using System.Linq;
using Jellyfin.Plugin.AmazonMusic.Catalog.Parsing;
using Xunit;

namespace Jellyfin.Plugin.AmazonMusic.Tests;

public class CatalogResponseParserTests
{
    [Fact]
    public void SearchTemplateSeparatesResourceKinds()
    {
        var body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Catalog", "search.json"));

        var songs = CatalogResponseParser.ParseSongs(body, "jp");
        var albums = CatalogResponseParser.ParseAlbums(body, "jp");
        var artists = CatalogResponseParser.ParseArtists(body, "jp");

        Assert.Single(songs);
        Assert.Equal("B000000003", songs[0].Id);
        Assert.Equal("B000000002", songs[0].Attributes.AlbumId);
        Assert.Null(songs[0].Attributes.AlbumName);
        Assert.Single(albums);
        Assert.Equal("B000000002", albums[0].Id);
        Assert.Single(artists);
        Assert.Equal("B000000001", artists[0].Id);
        Assert.Equal("https://images.example/artist.jpg", artists[0].Attributes.Artwork?.Url);
    }

    [Fact]
    public void GraphQlErrorsAreNotTreatedAsMissing()
        => Assert.Throws<CatalogProtocolException>(() => CatalogResponseParser.ParseSong("{\"errors\":[{\"message\":\"denied\"}]}", "jp"));

    [Fact]
    public void AlbumTracksKeepResponseOrder()
    {
        const string metadata = """
            {"data":{"album":{"id":"B000000002","title":"Album","trackCount":2,"images":[],"contributingArtists":{"edges":[]}}}}
            """;
        const string tracks = """
            {"data":{"album":{"id":"B000000002","tracks":[{"id":"B000000004","title":"First","trackNumber":1},{"id":"B000000003","title":"Second","trackNumber":1}]}}}
            """;

        var album = CatalogResponseParser.ParseAlbum(metadata, tracks, "jp");

        Assert.NotNull(album);
        Assert.Equal(["B000000004", "B000000003"], album.Tracks.Select(track => track.Id));
        Assert.Equal([1, 1], album.Tracks.Select(track => track.Attributes.TrackNumber));
    }
}
