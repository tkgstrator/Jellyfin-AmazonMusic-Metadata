using System.Text.Json;
using Jellyfin.Plugin.AmazonMusic.Catalog;
using Jellyfin.Plugin.AmazonMusic.Catalog.GraphQl;
using Xunit;

namespace Jellyfin.Plugin.AmazonMusic.Tests;

public class GraphQlRequestBuilderTests
{
    [Fact]
    public void Build_CreatesAnApolloPostRequest()
    {
        var request = GraphQlRequestBuilder.Build(
            "jp",
            "trackMetadata",
            AmazonMusicOperations.TrackMetadata,
            new { id = "TRACK" },
            CatalogRequestKind.Lookup,
            "track");

        Assert.Equal("POST", request.Method.Method);
        Assert.Equal("jp", request.Marketplace);
        Assert.StartsWith("graphql:v1:jp:trackMetadata:", request.CacheKey, System.StringComparison.Ordinal);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal("trackMetadata", body.RootElement.GetProperty("operationName").GetString());
        Assert.Equal("TRACK", body.RootElement.GetProperty("variables").GetProperty("id").GetString());
    }

    [Fact]
    public void AlbumTracks_SelectsTheTrackArrayDirectly()
    {
        Assert.Contains("tracks {", AmazonMusicOperations.GetAlbumTracks, System.StringComparison.Ordinal);
        Assert.DoesNotContain("tracks { edgeCount", AmazonMusicOperations.GetAlbumTracks, System.StringComparison.Ordinal);
        Assert.DoesNotContain("tracks { edges", AmazonMusicOperations.GetAlbumTracks, System.StringComparison.Ordinal);
    }

    [Fact]
    public void ArtistSummary_DoesNotRequestRestrictedTrackSummary()
    {
        Assert.DoesNotContain("tracks", AmazonMusicOperations.GetArtistSummary, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Build_SeparatesVariablesAndMarketplaces()
    {
        var first = Build("jp", "A");
        var second = Build("jp", "B");
        var otherMarketplace = Build("us", "A");

        Assert.NotEqual(first.CacheKey, second.CacheKey);
        Assert.NotEqual(first.CacheKey, otherMarketplace.CacheKey);
    }

    private static CatalogRequest Build(string marketplace, string id)
        => GraphQlRequestBuilder.Build(
            marketplace,
            "trackMetadata",
            AmazonMusicOperations.TrackMetadata,
            new { id },
            CatalogRequestKind.Lookup,
            "track");
}
