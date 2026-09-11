using System;

namespace Jellyfin.Plugin.AmazonMusic.Catalog.WebPlayer;

/// <summary>
/// Holds the anonymous web player values needed by the GraphQL transport.
/// </summary>
public sealed record WebPlayerBootstrap(
    MarketplaceDefinition Marketplace,
    Uri GraphQlEndpoint,
    Uri DragonflyBundle,
    string AnonymousApiKey,
    string AppVersion,
    string DeviceType,
    DateTimeOffset AcquiredAt);
