using System;
using System.Collections.Generic;
using Jellyfin.Plugin.AmazonMusic.Catalog;
using Jellyfin.Plugin.AmazonMusic.Catalog.Caching;
using Jellyfin.Plugin.AmazonMusic.Catalog.Throttling;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.AmazonMusic.Configuration;

/// <summary>
/// Order in which Amazon Music marketplaces are queried.
/// </summary>
public enum MarketplacePriority
{
    /// <summary>
    /// Query the Japanese marketplace first, fall back to the US marketplace.
    /// </summary>
    JapanThenUnitedStates,

    /// <summary>
    /// Query the US marketplace first, fall back to the Japanese marketplace.
    /// </summary>
    UnitedStatesThenJapan,

    /// <summary>
    /// Query the Japanese marketplace only.
    /// </summary>
    JapanOnly,

    /// <summary>
    /// Query the US marketplace only.
    /// </summary>
    UnitedStatesOnly
}

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        Marketplaces = MarketplacePriority.JapanThenUnitedStates;
        RequestTimeoutSeconds = 30;
        RequestIntervalMilliseconds = 1000;
        EnableCache = true;
        CacheLifetimeDays = 30;
        CacheNotFoundLifetimeHours = 24;
        MaxCacheMemoryMegabytes = 64;
        MaxPersistedEntryKilobytes = 8;
    }

    /// <summary>
    /// Gets or sets the marketplace query order.
    /// </summary>
    public MarketplacePriority Marketplaces { get; set; }

    /// <summary>
    /// Gets or sets the per-request timeout in seconds for catalog calls.
    /// </summary>
    public int RequestTimeoutSeconds { get; set; }

    /// <summary>
    /// Gets or sets the minimum time between two catalog requests, in
    /// milliseconds. Requests are serialized and spaced out to avoid bursts
    /// against the Web Player services.
    /// </summary>
    public int RequestIntervalMilliseconds { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether catalog responses are cached
    /// locally so the same lookup is not fetched twice.
    /// </summary>
    public bool EnableCache { get; set; }

    /// <summary>
    /// Gets or sets how many days a cached response stays usable.
    /// </summary>
    public int CacheLifetimeDays { get; set; }

    /// <summary>
    /// Gets or sets how many hours a "not found" answer is remembered.
    /// </summary>
    public int CacheNotFoundLifetimeHours { get; set; }

    /// <summary>
    /// Gets or sets the memory budget for cached responses, in megabytes.
    /// </summary>
    public int MaxCacheMemoryMegabytes { get; set; }

    /// <summary>
    /// Gets or sets the largest response written to disk, in kilobytes.
    /// Larger ones are kept in memory only.
    /// </summary>
    public int MaxPersistedEntryKilobytes { get; set; }

    /// <summary>
    /// Gets the marketplaces to query, in order.
    /// </summary>
    /// <returns>Ordered marketplace identifiers.</returns>
    public IReadOnlyList<string> GetMarketplaceOrder()
    {
        return Marketplaces switch
        {
            MarketplacePriority.JapanThenUnitedStates => [CatalogOptions.Japan, CatalogOptions.UnitedStates],
            MarketplacePriority.UnitedStatesThenJapan => [CatalogOptions.UnitedStates, CatalogOptions.Japan],
            MarketplacePriority.JapanOnly => [CatalogOptions.Japan],
            MarketplacePriority.UnitedStatesOnly => [CatalogOptions.UnitedStates],
            _ => [CatalogOptions.Japan, CatalogOptions.UnitedStates]
        };
    }

    /// <summary>
    /// Projects this configuration onto the Jellyfin-independent options used by
    /// the catalog layer.
    /// </summary>
    /// <returns>Catalog options.</returns>
    public CatalogOptions ToCatalogOptions()
    {
        return new CatalogOptions
        {
            Marketplaces = GetMarketplaceOrder(),
        };
    }

    /// <summary>
    /// Projects the request pacing settings onto the options used by the throttle.
    /// </summary>
    /// <returns>Throttle options.</returns>
    public ThrottleOptions ToThrottleOptions()
    {
        return new ThrottleOptions
        {
            MinInterval = TimeSpan.FromMilliseconds(Math.Max(0, RequestIntervalMilliseconds)),
        };
    }

    /// <summary>
    /// Projects the cache settings onto the options used by the cache itself.
    /// </summary>
    /// <returns>Cache options.</returns>
    public CatalogCacheOptions ToCacheOptions()
    {
        return new CatalogCacheOptions
        {
            Enabled = EnableCache,
            Lifetime = TimeSpan.FromDays(Math.Max(1, CacheLifetimeDays)),
            NegativeLifetime = TimeSpan.FromHours(Math.Max(1, CacheNotFoundLifetimeHours)),
            MaxMemoryBytes = Math.Max(1, MaxCacheMemoryMegabytes) * 1024L * 1024L,
            MaxPersistedEntryBytes = Math.Max(0, MaxPersistedEntryKilobytes) * 1024,
        };
    }
}
