using Jellyfin.Plugin.AmazonMusic.Configuration;
using Xunit;

namespace Jellyfin.Plugin.AmazonMusic.Tests;

public class PluginConfigurationTests
{
    [Theory]
    [InlineData(MarketplacePriority.JapanThenUnitedStates, new[] { "jp", "us" })]
    [InlineData(MarketplacePriority.UnitedStatesThenJapan, new[] { "us", "jp" })]
    [InlineData(MarketplacePriority.JapanOnly, new[] { "jp" })]
    [InlineData(MarketplacePriority.UnitedStatesOnly, new[] { "us" })]
    public void GetMarketplaceOrder_ReturnsExpectedOrder(MarketplacePriority priority, string[] expected)
    {
        var config = new PluginConfiguration { Marketplaces = priority };

        Assert.Equal(expected, config.GetMarketplaceOrder());
    }

    [Fact]
    public void ToCatalogOptions_CarriesEverySetting()
    {
        var config = new PluginConfiguration
        {
            Marketplaces = MarketplacePriority.UnitedStatesOnly,
            LanguageOverride = "en-gb",
            MaxSearchResults = 7,
            ArtworkSize = 600,
        };

        var options = config.ToCatalogOptions();

        Assert.Equal(["us"], options.Marketplaces);
        Assert.Equal("en-gb", options.LanguageOverride);
        Assert.Equal(7, options.MaxSearchResults);
        Assert.Equal(600, options.ArtworkSize);
    }
}
