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
    public void ToCatalogOptions_CarriesMarketplaceOrder()
    {
        var config = new PluginConfiguration
        {
            Marketplaces = MarketplacePriority.UnitedStatesOnly,
        };

        var options = config.ToCatalogOptions();

        Assert.Equal(["us"], options.Marketplaces);
    }
}
