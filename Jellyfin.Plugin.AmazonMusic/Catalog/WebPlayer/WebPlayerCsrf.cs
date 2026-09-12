namespace Jellyfin.Plugin.AmazonMusic.Catalog.WebPlayer;

/// <summary>
/// Holds the transient CSRF values issued to an anonymous Web Player session.
/// </summary>
public sealed record WebPlayerCsrf(string Token, string Timestamp, string RandomNonce);
