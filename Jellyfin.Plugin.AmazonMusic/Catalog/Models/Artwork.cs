namespace Jellyfin.Plugin.AmazonMusic.Catalog.Models;

/// <summary>
/// Describes an artwork URL returned by Amazon Music.
/// </summary>
/// <param name="Url">Opaque artwork URL.</param>
/// <param name="Width">Reported width, when available.</param>
/// <param name="Height">Reported height, when available.</param>
public sealed record Artwork(string Url, int? Width = null, int? Height = null);
