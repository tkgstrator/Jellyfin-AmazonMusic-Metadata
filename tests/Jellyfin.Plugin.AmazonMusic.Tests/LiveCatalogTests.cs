using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Plugin.AmazonMusic.Catalog;
using Jellyfin.Plugin.AmazonMusic.Catalog.GraphQl;
using Jellyfin.Plugin.AmazonMusic.Catalog.WebPlayer;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.AmazonMusic.Tests;

public class LiveCatalogTests
{
    private const string LiveSkip = "Network-dependent; remove Skip locally to exercise the public web player contract.";

    [Fact(Skip = LiveSkip)]
    public async Task JapaneseGuestBootstrapMatchesTheExpectedContract()
    {
        using var client = new HttpClient();
        using var provider = new WebPlayerBootstrapProvider(
            client,
            NullLogger<WebPlayerBootstrapProvider>.Instance);

        var bootstrap = await provider.GetAsync("jp", false, TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(bootstrap.AppVersion));
        Assert.False(string.IsNullOrWhiteSpace(bootstrap.AnonymousApiKey));
        Assert.Equal("JP", bootstrap.Marketplace.Territory);
        Assert.True(bootstrap.GraphQlEndpoint.IsAbsoluteUri);
    }

    [Fact(Skip = LiveSkip)]
    public async Task JapaneseGuestSearchReturnsJson()
    {
        using var client = new HttpClient();
        using var provider = new WebPlayerBootstrapProvider(
            client,
            NullLogger<WebPlayerBootstrapProvider>.Instance);
        var transport = new ShowSearchTransport(
            client,
            provider,
            () => TimeSpan.FromSeconds(30));
        var request = ShowSearchRequestBuilder.Build("jp", "aiko");

        var response = await transport.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(response));
        using var document = JsonDocument.Parse(response);
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
    }

    [Theory(Skip = LiveSkip)]
    [InlineData("trackMetadata", AmazonMusicOperations.TrackMetadata, "id", "B084VVMJQ6", "track")]
    [InlineData("getAlbumMetadata", AmazonMusicOperations.GetAlbumMetadata, "id", "B084VVLR9Q", "album")]
    [InlineData("getAlbumTracks", AmazonMusicOperations.GetAlbumTracks, "id", "B084VVLR9Q", "album")]
    [InlineData("getArtistSummary", AmazonMusicOperations.GetArtistSummary, "artistId", "B07ML4WR3G", "artist")]
    public async Task JapaneseGuestLookupMatchesTheExpectedSchema(
        string operationName,
        string query,
        string variableName,
        string id,
        string rootName)
    {
        using var client = new HttpClient();
        using var provider = new WebPlayerBootstrapProvider(
            client,
            NullLogger<WebPlayerBootstrapProvider>.Instance);
        var transport = new WebPlayerGraphQlTransport(
            client,
            provider,
            new WebPlayerIdentity(),
            () => TimeSpan.FromSeconds(30));
        var variables = variableName == "artistId" ? new { artistId = id } : (object)new { id };
        var request = GraphQlRequestBuilder.Build("jp", operationName, query, variables, CatalogRequestKind.Lookup, rootName);

        var response = await transport.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        using var document = JsonDocument.Parse(response);
        Assert.False(document.RootElement.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0);
        Assert.NotEqual(JsonValueKind.Null, document.RootElement.GetProperty("data").GetProperty(rootName).ValueKind);
    }
}
