using System;
using System.IO;
using System.Net.Http;
using Jellyfin.Plugin.AmazonMusic.Catalog;
using Jellyfin.Plugin.AmazonMusic.Catalog.Caching;
using Jellyfin.Plugin.AmazonMusic.Catalog.GraphQl;
using Jellyfin.Plugin.AmazonMusic.Catalog.Throttling;
using Jellyfin.Plugin.AmazonMusic.Catalog.WebPlayer;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
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
        serviceCollection.AddSingleton<WebPlayerIdentity>();
        serviceCollection.AddSingleton<IWebPlayerBootstrapProvider>(provider => new WebPlayerBootstrapProvider(
            CreateHttpClient(provider),
            provider.GetRequiredService<ILogger<WebPlayerBootstrapProvider>>()));
        serviceCollection.AddSingleton<ICatalogCache>(provider => new CatalogCache(
            CacheRoot(provider.GetRequiredService<IApplicationPaths>()),
            CurrentCacheOptions,
            provider.GetRequiredService<ILogger<CatalogCache>>()));
        serviceCollection.AddSingleton<IAmazonMusicCatalog>(provider =>
        {
            var bootstrap = provider.GetRequiredService<IWebPlayerBootstrapProvider>();
            var cache = provider.GetRequiredService<ICatalogCache>();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var timeout = CurrentRequestTimeout;
            var search = Compose(new ShowSearchTransport(CreateHttpClient(provider), bootstrap, timeout), cache, loggerFactory);
            var lookup = Compose(
                new WebPlayerGraphQlTransport(
                    CreateHttpClient(provider),
                    bootstrap,
                    provider.GetRequiredService<WebPlayerIdentity>(),
                    timeout),
                cache,
                loggerFactory);
            return new AmazonMusicCatalog(search, lookup, CurrentOptions);
        });
    }

    /// <summary>
    /// Wraps a network transport in the pacing and caching the catalog needs.
    /// </summary>
    /// <remarks>
    /// The chain is cache, then throttle, then network. The cache sits outside
    /// so hits are not paced; the throttle sits inside it so every request that
    /// really leaves the plugin is paced, including the ones a cache miss
    /// issues.
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

    private static TimeSpan CurrentRequestTimeout()
        => TimeSpan.FromSeconds(Math.Max(1, Plugin.Instance?.Configuration.RequestTimeoutSeconds ?? 30));

    private static HttpClient CreateHttpClient(IServiceProvider provider)
        => provider.GetRequiredService<IHttpClientFactory>().CreateClient(NamedClient.Default);

    private static string CacheRoot(IApplicationPaths paths)
        => Path.Combine(paths.CachePath, "amazon-music");
}
