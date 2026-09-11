using System.Net.Http;
using System.Threading.Tasks;
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
}
