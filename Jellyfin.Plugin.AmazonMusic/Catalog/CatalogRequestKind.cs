namespace Jellyfin.Plugin.AmazonMusic.Catalog;

/// <summary>
/// Classifies catalog requests for independent rate-limit cooldowns.
/// </summary>
public enum CatalogRequestKind
{
    /// <summary>
    /// A catalog search.
    /// </summary>
    Search,

    /// <summary>
    /// A lookup by catalog identifier.
    /// </summary>
    Lookup
}
