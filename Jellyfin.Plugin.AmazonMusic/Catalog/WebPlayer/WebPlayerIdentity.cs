using System;
using System.Globalization;
using System.Security.Cryptography;

namespace Jellyfin.Plugin.AmazonMusic.Catalog.WebPlayer;

/// <summary>
/// Provides process-scoped anonymous web player identifiers.
/// </summary>
public sealed class WebPlayerIdentity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebPlayerIdentity"/> class.
    /// </summary>
    public WebPlayerIdentity()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        DeviceId = Convert.ToHexString(bytes).ToLowerInvariant();
        SessionId = string.Create(
            CultureInfo.InvariantCulture,
            $"{RandomNumberGenerator.GetInt32(100, 1000)}-{RandomNumberGenerator.GetInt32(1000000, 10000000)}-{RandomNumberGenerator.GetInt32(1000000, 10000000)}");
    }

    /// <summary>
    /// Gets the identifier kept stable for the plugin process.
    /// </summary>
    public string DeviceId { get; }

    /// <summary>
    /// Gets the anonymous session identifier.
    /// </summary>
    public string SessionId { get; }
}
