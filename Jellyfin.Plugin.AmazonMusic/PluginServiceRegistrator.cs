using System.IO;
using Jellyfin.Plugin.AmazonMusic.Catalog;
using Jellyfin.Plugin.AmazonMusic.Catalog.Caching;
using Jellyfin.Plugin.AmazonMusic.Catalog.Throttling;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AmazonMusic;

/// <summary>
/// Registers the catalog layer so the providers can take it by constructor
/// injection.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<ICatalogCache>(provider => new CatalogCache(
            CacheRoot(provider.GetRequiredService<IApplicationPaths>()),
            CurrentCacheOptions,
            provider.GetRequiredService<ILogger<CatalogCache>>()));
    }

    /// <summary>
    /// Wraps a network transport in the pacing and caching the catalog needs.
    /// </summary>
    /// <remarks>
    /// The chain is cache, then throttle, then network. The cache sits outside
    /// so hits are not paced; the throttle sits inside it so every request that
    /// really leaves the plugin is paced, including the ones a cache miss
    /// issues.
    /// <para>
    /// The network transport itself is the one piece still missing: how the
    /// Amazon Music catalog is reached has not been settled (see the README).
    /// Once it exists, register it and hand it to this method, and everything
    /// above it works unchanged.
    /// </para>
    /// </remarks>
    /// <param name="network">Transport that performs the actual request.</param>
    /// <param name="cache">Response cache.</param>
    /// <param name="loggerFactory">Logger factory.</param>
    /// <returns>The composed transport.</returns>
    public static ICatalogTransport Compose(
        ICatalogTransport network,
        ICatalogCache cache,
        ILoggerFactory loggerFactory)
        => new CachingCatalogTransport(
            new ThrottledCatalogTransport(
                network,
                CurrentThrottleOptions,
                loggerFactory.CreateLogger<ThrottledCatalogTransport>()),
            cache,
            loggerFactory.CreateLogger<CachingCatalogTransport>());

    /// <summary>
    /// Reads the options afresh on every call, so changes made on the
    /// configuration page take effect without a server restart.
    /// </summary>
    /// <returns>The current catalog options.</returns>
    public static CatalogOptions CurrentOptions()
        => Plugin.Instance?.Configuration.ToCatalogOptions() ?? new CatalogOptions();

    private static ThrottleOptions CurrentThrottleOptions()
        => Plugin.Instance?.Configuration.ToThrottleOptions() ?? new ThrottleOptions();

    private static CatalogCacheOptions CurrentCacheOptions()
        => Plugin.Instance?.Configuration.ToCacheOptions() ?? new CatalogCacheOptions();

    private static string CacheRoot(IApplicationPaths paths)
        => Path.Combine(paths.CachePath, "amazon-music");
}
