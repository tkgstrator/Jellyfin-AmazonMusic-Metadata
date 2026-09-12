using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.AmazonMusic.Catalog;
using Jellyfin.Plugin.AmazonMusic.Catalog.Throttling;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.AmazonMusic.Tests;

public class ThrottledCatalogTransportTests
{
    [Fact]
    public async Task SendAsync_SendsOneRequestAtATime()
    {
        var inner = new ScriptedTransport(delay: TimeSpan.FromMilliseconds(30));
        using var transport = Build(inner, new ThrottleOptions { MinInterval = TimeSpan.Zero });

        var lookups = Enumerable.Range(0, 5)
            .Select(i => transport.SendAsync(Request($"/v1/{i}"), TestContext.Current.CancellationToken));
        await Task.WhenAll(lookups);

        Assert.Equal(1, inner.MaxConcurrency);
        Assert.Equal(5, inner.Calls);
    }

    [Fact]
    public async Task SendAsync_SpacesRequestsByTheMinimumInterval()
    {
        var interval = TimeSpan.FromMilliseconds(80);
        var inner = new ScriptedTransport();
        using var transport = Build(inner, new ThrottleOptions { MinInterval = interval });

        await transport.SendAsync(Request("/v1/a"), TestContext.Current.CancellationToken);
        await transport.SendAsync(Request("/v1/b"), TestContext.Current.CancellationToken);
        await transport.SendAsync(Request("/v1/c"), TestContext.Current.CancellationToken);

        var gaps = inner.Starts.Zip(inner.Starts.Skip(1), (earlier, later) => later - earlier);
        Assert.All(gaps, gap => Assert.True(gap >= interval - TimeSpan.FromMilliseconds(15), $"gap {gap} shorter than {interval}"));
    }

    [Fact]
    public async Task SendAsync_RetriesAfterTheCooldownWhenRateLimited()
    {
        var inner = new ScriptedTransport(rateLimitedCalls: 1);
        using var transport = Build(inner, new ThrottleOptions
        {
            MinInterval = TimeSpan.Zero,
            InitialCooldown = TimeSpan.FromMilliseconds(60),
            MaxAttempts = 3,
        });

        var body = await transport.SendAsync(Request("/v1/a"), TestContext.Current.CancellationToken);

        Assert.Equal("ok", body);
        Assert.Equal(2, inner.Calls);
        Assert.True(inner.Starts[1] - inner.Starts[0] >= TimeSpan.FromMilliseconds(45));
    }

    [Fact]
    public async Task SendAsync_GivesUpAfterTheConfiguredAttempts()
    {
        var inner = new ScriptedTransport(rateLimitedCalls: int.MaxValue);
        using var transport = Build(inner, new ThrottleOptions
        {
            MinInterval = TimeSpan.Zero,
            InitialCooldown = TimeSpan.FromMilliseconds(10),
            MaxCooldown = TimeSpan.FromSeconds(10),
            MaxAttempts = 3,
        });

        await Assert.ThrowsAsync<CatalogRateLimitedException>(
            () => transport.SendAsync(Request("/v1/a"), TestContext.Current.CancellationToken));

        Assert.Equal(3, inner.Calls);
        Assert.True(transport.IsCoolingDown);
    }

    [Fact]
    public async Task SendAsync_PausesOtherLookupsDuringTheCooldown()
    {
        var inner = new ScriptedTransport(rateLimitedCalls: 1);
        using var transport = Build(inner, new ThrottleOptions
        {
            MinInterval = TimeSpan.Zero,
            InitialCooldown = TimeSpan.FromMilliseconds(80),
            MaxAttempts = 2,
        });

        var first = transport.SendAsync(Request("/v1/a"), TestContext.Current.CancellationToken);
        await inner.FirstCallStarted.Task;
        var second = transport.SendAsync(Request("/v1/b"), TestContext.Current.CancellationToken);
        await Task.WhenAll(first, second);

        Assert.Equal(3, inner.Calls);
        Assert.All(inner.Starts.Skip(1), start => Assert.True(start - inner.Starts[0] >= TimeSpan.FromMilliseconds(65)));
    }

    [Fact]
    public async Task SendAsync_FailsFastWhileTheCatalogKeepsRefusing()
    {
        var inner = new ScriptedTransport(rateLimitedCalls: int.MaxValue);
        using var transport = Build(inner, new ThrottleOptions
        {
            MinInterval = TimeSpan.Zero,
            InitialCooldown = TimeSpan.FromSeconds(10),
            MaxCooldown = TimeSpan.FromSeconds(10),
            MaxAttempts = 1,
        });

        await Assert.ThrowsAsync<CatalogRateLimitedException>(
            () => transport.SendAsync(Request("/v1/a"), TestContext.Current.CancellationToken));

        var started = DateTimeOffset.UtcNow;
        await Assert.ThrowsAsync<CatalogRateLimitedException>(
            () => transport.SendAsync(Request("/v1/b"), TestContext.Current.CancellationToken));

        Assert.True(DateTimeOffset.UtcNow - started < TimeSpan.FromSeconds(2));
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task SendAsync_ResetsTheCooldownAfterASuccess()
    {
        var inner = new ScriptedTransport(rateLimitedCalls: 1);
        using var transport = Build(inner, new ThrottleOptions
        {
            MinInterval = TimeSpan.Zero,
            InitialCooldown = TimeSpan.FromMilliseconds(20),
            MaxAttempts = 2,
        });

        await transport.SendAsync(Request("/v1/a"), TestContext.Current.CancellationToken);

        Assert.False(transport.IsCoolingDown);
    }

    [Fact]
    public async Task SendAsync_DoesNotLetASearchCooldownBlockIdLookups()
    {
        var inner = new ScriptedTransport(rateLimitedCalls: int.MaxValue, refusedKind: CatalogRequestKind.Search);
        using var transport = Build(inner, new ThrottleOptions
        {
            MinInterval = TimeSpan.Zero,
            InitialCooldown = TimeSpan.FromSeconds(10),
            MaxCooldown = TimeSpan.FromSeconds(10),
            MaxAttempts = 1,
        });

        await Assert.ThrowsAsync<CatalogRateLimitedException>(
            () => transport.SendAsync(Request("/graphql/search", CatalogRequestKind.Search), TestContext.Current.CancellationToken));

        var started = DateTimeOffset.UtcNow;
        Assert.Equal("ok", await transport.SendAsync(Request("/graphql/lookup"), TestContext.Current.CancellationToken));
        Assert.True(DateTimeOffset.UtcNow - started < TimeSpan.FromSeconds(2));
        Assert.True(transport.IsCoolingDown);
    }

    private static CatalogRequest Request(string relativeUrl, CatalogRequestKind kind = CatalogRequestKind.Lookup)
        => new(HttpMethod.Post, relativeUrl, "{}", relativeUrl, kind);

    private static ThrottledCatalogTransport Build(ICatalogTransport inner, ThrottleOptions options)
        => new(inner, () => options, NullLogger<ThrottledCatalogTransport>.Instance);

    private sealed class ScriptedTransport : ICatalogTransport
    {
        private readonly TimeSpan _delay;
        private readonly object _lock = new();
        private readonly CatalogRequestKind? _refusedKind;
        private int _rateLimitedCalls;
        private int _inFlight;

        public ScriptedTransport(TimeSpan delay = default, int rateLimitedCalls = 0, CatalogRequestKind? refusedKind = null)
        {
            _delay = delay;
            _rateLimitedCalls = rateLimitedCalls;
            _refusedKind = refusedKind;
        }

        public int Calls => Starts.Count;

        public int MaxConcurrency { get; private set; }

        public List<DateTimeOffset> Starts { get; } = [];

        public TaskCompletionSource FirstCallStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<string?> SendAsync(CatalogRequest request, CancellationToken cancellationToken)
        {
            bool refuse;
            lock (_lock)
            {
                Starts.Add(DateTimeOffset.UtcNow);
                _inFlight++;
                MaxConcurrency = Math.Max(MaxConcurrency, _inFlight);
                refuse = _rateLimitedCalls > 0 && (_refusedKind is null || request.Kind == _refusedKind);
                if (refuse)
                {
                    _rateLimitedCalls--;
                }
            }

            FirstCallStarted.TrySetResult();

            try
            {
                if (_delay > TimeSpan.Zero)
                {
                    await Task.Delay(_delay, cancellationToken);
                }

                if (refuse)
                {
                    throw new CatalogRateLimitedException();
                }

                return "ok";
            }
            finally
            {
                lock (_lock)
                {
                    _inFlight--;
                }
            }
        }
    }
}
