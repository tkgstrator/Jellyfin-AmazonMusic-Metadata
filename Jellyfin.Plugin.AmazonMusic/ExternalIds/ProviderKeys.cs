namespace Jellyfin.Plugin.AmazonMusic.ExternalIds;

/// <summary>
/// Keys this plugin writes into <c>ProviderIds</c>.
/// </summary>
public static class ProviderKeys
{
    /// <summary>
    /// Amazon Music catalog identifier of a song.
    /// </summary>
    public const string Song = "AmazonMusicSong";

    /// <summary>
    /// Amazon Music catalog identifier of an album.
    /// </summary>
    public const string Album = "AmazonMusicAlbum";

    /// <summary>
    /// Amazon Music catalog identifier of an artist.
    /// </summary>
    public const string Artist = "AmazonMusicArtist";

    /// <summary>
    /// Marketplace the stored identifiers belong to.
    /// </summary>
    /// <remarks>
    /// Catalog identifiers are marketplace-scoped, so the marketplace has to be
    /// remembered alongside them. No <c>IExternalId</c> implements this key, so
    /// it stays out of the item's external links in the UI.
    /// </remarks>
    public const string Marketplace = "AmazonMusicMarketplace";
}
