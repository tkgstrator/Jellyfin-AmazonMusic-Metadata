using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.AmazonMusic.Catalog.WebPlayer;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.AmazonMusic.Tests;

public class WebPlayerBootstrapProviderTests
{
    [Fact]
    public async Task GetAsync_PostsConfigAndCachesTheSnapshot()
    {
        var calls = 0;
        var handler = new DelegateHandler(request =>
        {
            calls++;
            if (request.RequestUri!.AbsolutePath == "/config.json")
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                return Json("""
                    {"version":"test","deviceType":"device","dragonflyBundle":"https://assets.example.invalid/dragonfly.js"}
                    """);
            }

            Assert.Equal(HttpMethod.Get, request.Method);
            return Text("const FIREFLY_ANONYMOUS_WEB_API_KEY = \"TEST_KEY\";");
        });
        using var provider = new WebPlayerBootstrapProvider(
            new HttpClient(handler),
            NullLogger<WebPlayerBootstrapProvider>.Instance);

        var first = provider.GetAsync("jp", false, TestContext.Current.CancellationToken);
        var second = provider.GetAsync("jp", false, TestContext.Current.CancellationToken);
        var snapshots = await Task.WhenAll(first, second);

        Assert.Same(snapshots[0], snapshots[1]);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task GetAsync_ForceRefreshFetchesAgain()
    {
        var calls = 0;
        var handler = new DelegateHandler(request =>
        {
            calls++;
            return request.RequestUri!.AbsolutePath == "/config.json"
                ? Json("{\"version\":\"test\",\"deviceType\":\"device\",\"dragonflyBundle\":\"https://assets.example.invalid/dragonfly.js\"}")
                : Text("const FIREFLY_ANONYMOUS_WEB_API_KEY = \"TEST_KEY\";");
        });
        using var provider = new WebPlayerBootstrapProvider(
            new HttpClient(handler),
            NullLogger<WebPlayerBootstrapProvider>.Instance);

        await provider.GetAsync("jp", false, TestContext.Current.CancellationToken);
        await provider.GetAsync("jp", true, TestContext.Current.CancellationToken);

        Assert.Equal(4, calls);
    }

    private static HttpResponseMessage Json(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Text(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response(request));
    }
}
