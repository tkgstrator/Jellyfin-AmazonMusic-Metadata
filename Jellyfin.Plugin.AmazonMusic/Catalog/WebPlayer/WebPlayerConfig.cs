using System;

namespace Jellyfin.Plugin.AmazonMusic.Catalog.WebPlayer;

/// <summary>
/// Holds the non-secret values parsed from the guest runtime configuration.
/// </summary>
public sealed record WebPlayerConfig(
    Uri DragonflyBundle,
    string AppVersion,
    string DeviceType,
    string DeviceId,
    string SessionId,
    WebPlayerCsrf Csrf,
    Uri GraphQlEndpoint);
