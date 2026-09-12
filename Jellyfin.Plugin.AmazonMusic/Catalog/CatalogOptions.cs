using System.Collections.Generic;

namespace Jellyfin.Plugin.AmazonMusic.Catalog;

/// <summary>
/// Catalog lookup settings. Deliberately free of any Jellyfin dependency so the
/// catalog layer stays unit testable.
/// </summary>
public class CatalogOptions
{
    /// <summary>
    /// Marketplace identifier for Japan.
    /// </summary>
    public const string Japan = "jp";

    /// <summary>
    /// Marketplace identifier for the United States.
    /// </summary>
    public const string UnitedStates = "us";

    /// <summary>
    /// Gets or sets the marketplaces to query, in order. The first one that
    /// yields a result wins.
    /// </summary>
    public IReadOnlyList<string> Marketplaces { get; set; } = [Japan, UnitedStates];
}
