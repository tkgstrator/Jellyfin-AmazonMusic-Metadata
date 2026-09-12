using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.AmazonMusic.Catalog;
using Jellyfin.Plugin.AmazonMusic.Catalog.GraphQl;
using Jellyfin.Plugin.AmazonMusic.Catalog.WebPlayer;
using Xunit;

namespace Jellyfin.Plugin.AmazonMusic.Tests;

public class WebPlayerGraphQlTransportTests
{
    [Fact]
    public async Task SendAsync_SendsOnlyAnonymousWebPlayerHeaders()
    {
        HttpRequestMessage? captured = null;
        var handler = new DelegateHandler(request =>
        {
            captured = Clone(request);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"data\":{}}") };
        });
        var transport = Create(handler, new FakeBootstrapProvider());

        var body = await transport.SendAsync(Request(), TestContext.Current.CancellationToken);

        Assert.Equal("{\"data\":{}}", body);
        Assert.NotNull(captured);
        Assert.Equal("https://gql.music.amazon.dev/", captured.RequestUri!.AbsoluteUri);
        Assert.True(captured.Headers.Contains("x-api-key"));
        Assert.True(captured.Headers.Contains("x-amzn-device-id"));
        Assert.True(captured.Headers.Contains("music-territory"));
        Assert.Null(captured.Headers.Authorization);
        Assert.False(captured.Headers.Contains("Cookie"));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task SendAsync_RefreshesBootstrapOnce(HttpStatusCode status)
    {
        var calls = 0;
        var handler = new DelegateHandler(_ => new HttpResponseMessage(++calls == 1 ? status : HttpStatusCode.OK)
        {
            Content = new StringContent("{\"data\":{}}"),
        });
        var bootstrap = new FakeBootstrapProvider();
        var transport = Create(handler, bootstrap);

        await transport.SendAsync(Request(), TestContext.Current.CancellationToken);

        Assert.Equal(2, calls);
        Assert.Equal([false, true], bootstrap.Refreshes);
    }

    [Fact]
    public async Task SendAsync_MapsRateLimitToTheCatalogException()
    {
        var handler = new DelegateHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        var transport = Create(handler, new FakeBootstrapProvider());

        await Assert.ThrowsAsync<CatalogRateLimitedException>(
            () => transport.SendAsync(Request(), TestContext.Current.CancellationToken));
    }

    private static WebPlayerGraphQlTransport Create(HttpMessageHandler handler, IWebPlayerBootstrapProvider bootstrap)
        => new(new HttpClient(handler), bootstrap, new WebPlayerIdentity(), () => TimeSpan.FromSeconds(5));

    private static CatalogRequest Request()
        => GraphQlRequestBuilder.Build("jp", "trackMetadata", AmazonMusicOperations.TrackMetadata, new { id = "A" }, CatalogRequestKind.Lookup, "track");

    private static HttpRequestMessage Clone(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }

    private sealed class FakeBootstrapProvider : IWebPlayerBootstrapProvider
    {
        public List<bool> Refreshes { get; } = [];

        public Task<WebPlayerBootstrap> GetAsync(string marketplace, bool forceRefresh, CancellationToken cancellationToken)
        {
            Refreshes.Add(forceRefresh);
            var definition = MarketplaceDefinition.Resolve(marketplace);
            return Task.FromResult(new WebPlayerBootstrap(
                definition,
                new Uri("https://gql.music.amazon.dev"),
                new Uri("https://assets.example.invalid/dragonfly.js"),
                "TEST_KEY",
                "test-version",
                "TEST_DEVICE",
                "TEST_DEVICE_ID",
                "TEST_SESSION_ID",
                new WebPlayerCsrf("TEST_TOKEN", "0", "0"),
                DateTimeOffset.UtcNow));
        }
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response(request));
    }
}
