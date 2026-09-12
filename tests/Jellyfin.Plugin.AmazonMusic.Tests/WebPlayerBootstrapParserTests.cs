using System;
using System.IO;
using Jellyfin.Plugin.AmazonMusic.Catalog.WebPlayer;
using Xunit;

namespace Jellyfin.Plugin.AmazonMusic.Tests;

public class WebPlayerBootstrapParserTests
{
    [Fact]
    public void ParseConfig_ReadsTheRedactedJapaneseFixture()
    {
        var json = File.ReadAllText(Fixture("config-jp.json"));

        var config = WebPlayerBootstrapParser.ParseConfig(json, MarketplaceDefinition.Resolve("jp"));

        Assert.Equal("0.0.0-test", config.AppVersion);
        Assert.Equal("TEST_DEVICE_TYPE", config.DeviceType);
        Assert.Equal("https://assets.example.invalid/release/web-platform/dragonfly.test.js", config.DragonflyBundle.AbsoluteUri);
        Assert.Equal("https://gql.music.amazon.dev/", config.GraphQlEndpoint.AbsoluteUri);
    }

    [Fact]
    public void ParseConfig_UsesCountryEndpointWhenEnabled()
    {
        const string json = """
            {"version":"test","deviceType":"test","deviceId":"device-id","sessionId":"session-id","csrf":{"token":"token","ts":"1","rnd":"2"},"dragonflyBundle":"/dragonfly.js","isDragonflyFFCountryDomainEnabled":true}
            """;

        var config = WebPlayerBootstrapParser.ParseConfig(json, MarketplaceDefinition.Resolve("jp"));

        Assert.Equal("https://gql.music.amazon.co.jp/", config.GraphQlEndpoint.AbsoluteUri);
        Assert.Equal("https://music.amazon.co.jp/dragonfly.js", config.DragonflyBundle.AbsoluteUri);
    }

    [Fact]
    public void ExtractAnonymousApiKey_ReadsTheSyntheticFixture()
    {
        var javascript = File.ReadAllText(Fixture("dragonfly.js"));

        Assert.Equal("TEST_ANONYMOUS_API_KEY", WebPlayerBootstrapParser.ExtractAnonymousApiKey(javascript));
    }

    [Fact]
    public void ExtractAnonymousApiKey_DoesNotIncludeBundleTextInFailure()
    {
        const string marker = "SENSITIVE_BUNDLE_TEXT";

        var exception = Assert.Throws<InvalidOperationException>(
            () => WebPlayerBootstrapParser.ExtractAnonymousApiKey(marker));

        Assert.DoesNotContain(marker, exception.Message, StringComparison.Ordinal);
    }

    private static string Fixture(string name)
        => Path.Combine(AppContext.BaseDirectory, "Fixtures", "WebPlayer", name);
}
