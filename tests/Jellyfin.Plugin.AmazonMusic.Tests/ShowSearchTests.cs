using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.AmazonMusic.Catalog;
using Jellyfin.Plugin.AmazonMusic.Catalog.WebPlayer;
using Xunit;

namespace Jellyfin.Plugin.AmazonMusic.Tests;

public class ShowSearchTests
{
    [Fact]
    public void BuilderKeepsTransientValuesOutOfTheCacheKey()
    {
        var request = ShowSearchRequestBuilder.Build("jp", "test");

        Assert.Equal(CatalogRequestKind.Search, request.Kind);
        Assert.StartsWith("show-search:v1:jp:", request.CacheKey, StringComparison.Ordinal);
        Assert.DoesNotContain("SESSION", request.CacheKey, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TransportBuildsTheObservedDoubleEncodedPayload()
    {
        HttpRequestMessage? captured = null;
        string? body = null;
        var handler = new DelegateHandler(async request =>
        {
            captured = Clone(request);
            body = await request.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        });
        using var client = new HttpClient(handler);
        var transport = new ShowSearchTransport(
            client,
            new FakeBootstrapProvider(),
            () => TimeSpan.FromSeconds(5),
            new FixedTimeProvider());

        await transport.SendAsync(ShowSearchRequestBuilder.Build("jp", "test"), TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal("https://fe.web.skill.music.a2z.com/api/showSearch", captured.RequestUri!.AbsoluteUri);
        Assert.Equal("text/plain; charset=utf-8", captured.Content!.Headers.ContentType!.ToString());
        Assert.Equal("https://music.amazon.co.jp", captured.Headers.GetValues("Origin").Single());
        using var outer = JsonDocument.Parse(body!);
        Assert.Equal(3, outer.RootElement.EnumerateObject().Count());
        using var keyword = JsonDocument.Parse(outer.RootElement.GetProperty("keyword").GetString()!);
        Assert.Equal("test", keyword.RootElement.GetProperty("keyword").GetString());
        using var userHash = JsonDocument.Parse(outer.RootElement.GetProperty("userHash").GetString()!);
        Assert.Equal("LIBRARY_MEMBER", userHash.RootElement.GetProperty("level").GetString());
        using var headers = JsonDocument.Parse(outer.RootElement.GetProperty("headers").GetString()!);
        using var authentication = JsonDocument.Parse(headers.RootElement.GetProperty("x-amzn-authentication").GetString()!);
        Assert.Equal(string.Empty, authentication.RootElement.GetProperty("accessToken").GetString());
        Assert.Equal("TEST_SESSION_ID", headers.RootElement.GetProperty("x-amzn-session-id").GetString());
        Assert.Equal("TEST_CSRF_TOKEN", JsonDocument.Parse(headers.RootElement.GetProperty("x-amzn-csrf").GetString()!).RootElement.GetProperty("token").GetString());
    }

    private static HttpRequestMessage Clone(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        clone.Content = new StringContent(string.Empty);
        foreach (var header in request.Content!.Headers)
        {
            clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }

    private sealed class FakeBootstrapProvider : IWebPlayerBootstrapProvider
    {
        public Task<WebPlayerBootstrap> GetAsync(string marketplace, bool forceRefresh, CancellationToken cancellationToken)
            => Task.FromResult(new WebPlayerBootstrap(
                MarketplaceDefinition.Resolve(marketplace),
                new Uri("https://gql.music.amazon.dev"),
                new Uri("https://assets.example.invalid/dragonfly.js"),
                "TEST_API_KEY",
                "test-version",
                "TEST_DEVICE_TYPE",
                "TEST_DEVICE_ID",
                "TEST_SESSION_ID",
                new WebPlayerCsrf("TEST_CSRF_TOKEN", "1", "2"),
                DateTimeOffset.UnixEpoch));
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => response(request);
    }
}
