using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.AmazonMusic.Catalog;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.AmazonMusic.ExternalIds;

/// <summary>
/// Builds the public Amazon Music links shown on an item.
/// </summary>
/// <remarks>
/// A separate provider rather than a format string on the external ids,
/// because a correct link needs the marketplace as well as the identifier, and
/// the two are stored under different keys.
/// </remarks>
public class AmazonMusicExternalUrlProvider : IExternalUrlProvider
{
    /// <inheritdoc />
    public string Name => PluginConstants.Name;

    /// <inheritdoc />
    public IEnumerable<string> GetExternalUrls(BaseItem item)
    {
        var marketplace = item.GetProviderId(ProviderKeys.Marketplace);
        if (string.IsNullOrEmpty(marketplace))
        {
            marketplace = CatalogOptions.UnitedStates;
        }

        var key = item switch
        {
            MusicAlbum => ProviderKeys.Album,
            MusicArtist => ProviderKeys.Artist,
            Audio => ProviderKeys.Song,
            _ => null
        };

        if (key is null)
        {
            yield break;
        }

        var id = item.GetProviderId(key);
        if (string.IsNullOrEmpty(id))
        {
            yield break;
        }

        // Amazon addresses albums and artists directly; a track is reached
        // through its album, so a song id links to the track page.
        var path = key switch
        {
            ProviderKeys.Album => "albums",
            ProviderKeys.Artist => "artists",
            _ => "tracks"
        };

        yield return string.Format(
            CultureInfo.InvariantCulture,
            "{0}/{1}/{2}/{3}",
            PluginConstants.WebBaseUrl,
            marketplace,
            path,
            id);
    }
}
