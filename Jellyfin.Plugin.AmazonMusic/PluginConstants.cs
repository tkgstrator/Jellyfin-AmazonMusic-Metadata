namespace Jellyfin.Plugin.AmazonMusic;

/// <summary>
/// Names and URLs shared across the plugin.
/// </summary>
public static class PluginConstants
{
    /// <summary>
    /// Display name, also used as the provider name Jellyfin shows in library
    /// settings and on external links.
    /// </summary>
    public const string Name = "Amazon Music";

    /// <summary>
    /// Base URL of the public Amazon Music web player. The marketplace is a
    /// path segment on top of this, so external links can point at the same
    /// marketplace an identifier was found in.
    /// </summary>
    public const string WebBaseUrl = "https://music.amazon.com";
}
