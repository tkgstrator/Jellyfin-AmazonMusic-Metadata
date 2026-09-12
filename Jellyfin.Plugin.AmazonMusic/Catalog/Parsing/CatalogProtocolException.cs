using System;

namespace Jellyfin.Plugin.AmazonMusic.Catalog.Parsing;

/// <summary>
/// Indicates that Amazon Music returned a response which violated the observed catalog contract.
/// </summary>
public sealed class CatalogProtocolException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogProtocolException"/> class.
    /// </summary>
    /// <param name="message">Safe diagnostic message.</param>
    public CatalogProtocolException(string message)
        : base(message)
    {
    }
}
