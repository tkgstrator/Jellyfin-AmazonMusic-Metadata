using Jellyfin.Plugin.AmazonMusic.Organizer;
using Xunit;

namespace Jellyfin.Plugin.AmazonMusic.Tests;

public class FolderTagTests
{
    [Theory]
    [InlineData("Album-[amzn-B084VVLR9Q]", "B084VVLR9Q")]
    [InlineData("Artist [AMZN-B07ML4WR3G]", "B07ML4WR3G")]
    [InlineData("Album", null)]
    public void ParseReadsAmazonAsin(string name, string? expected)
        => Assert.Equal(expected, FolderTag.Parse(name));
}
