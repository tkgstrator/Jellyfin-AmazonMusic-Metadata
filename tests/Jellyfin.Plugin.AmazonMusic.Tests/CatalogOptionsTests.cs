using Jellyfin.Plugin.AmazonMusic.Catalog;
using Xunit;

namespace Jellyfin.Plugin.AmazonMusic.Tests;

public class CatalogOptionsTests
{
    [Fact]
    public void DefaultsToJapaneseThenUnitedStatesMarketplace()
    {
        var options = new CatalogOptions();

        Assert.Equal(["jp", "us"], options.Marketplaces);
    }
}
