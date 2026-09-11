using System.Linq;
using Jellyfin.Plugin.AmazonMusic.ExternalIds;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Model.Entities;
using Xunit;

namespace Jellyfin.Plugin.AmazonMusic.Tests;

public class AmazonMusicExternalUrlProviderTests
{
    [Fact]
    public void GetExternalUrls_BuildsAMarketplaceAwareAlbumLink()
    {
        var album = new MusicAlbum();
        album.SetProviderId(ProviderKeys.Album, "1440791809");
        album.SetProviderId(ProviderKeys.Marketplace, "jp");

        var url = Assert.Single(new AmazonMusicExternalUrlProvider().GetExternalUrls(album));

        Assert.Equal("https://music.amazon.com/jp/albums/1440791809", url);
    }

    [Fact]
    public void GetExternalUrls_BuildsAnArtistLink()
    {
        var artist = new MusicArtist();
        artist.SetProviderId(ProviderKeys.Artist, "530814268");
        artist.SetProviderId(ProviderKeys.Marketplace, "us");

        var url = Assert.Single(new AmazonMusicExternalUrlProvider().GetExternalUrls(artist));

        Assert.Equal("https://music.amazon.com/us/artists/530814268", url);
    }

    [Fact]
    public void GetExternalUrls_BuildsASongLink()
    {
        var song = new Audio();
        song.SetProviderId(ProviderKeys.Song, "1837658529");
        song.SetProviderId(ProviderKeys.Marketplace, "jp");

        var url = Assert.Single(new AmazonMusicExternalUrlProvider().GetExternalUrls(song));

        Assert.Equal("https://music.amazon.com/jp/tracks/1837658529", url);
    }

    [Fact]
    public void GetExternalUrls_DefaultsToUsWhenTheMarketplaceIsUnknown()
    {
        var album = new MusicAlbum();
        album.SetProviderId(ProviderKeys.Album, "1440791809");

        var url = Assert.Single(new AmazonMusicExternalUrlProvider().GetExternalUrls(album));

        Assert.Equal("https://music.amazon.com/us/albums/1440791809", url);
    }

    [Fact]
    public void GetExternalUrls_YieldsNothingWithoutAnIdentifier()
    {
        var album = new MusicAlbum();

        Assert.Empty(new AmazonMusicExternalUrlProvider().GetExternalUrls(album));
    }
}
