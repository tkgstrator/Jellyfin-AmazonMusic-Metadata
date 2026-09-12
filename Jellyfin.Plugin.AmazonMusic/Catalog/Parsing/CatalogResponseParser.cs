using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Jellyfin.Plugin.AmazonMusic.Catalog.Models;

namespace Jellyfin.Plugin.AmazonMusic.Catalog.Parsing;

/// <summary>
/// Parses Web Player search templates and GraphQL catalog responses.
/// </summary>
internal static class CatalogResponseParser
{
    private enum SearchKind
    {
        Song,
        Album,
        Artist,
    }

    /// <summary>Parses song search results.</summary>
    /// <param name="body">Raw response body.</param>
    /// <param name="marketplace">Source marketplace.</param>
    /// <returns>Parsed songs.</returns>
    internal static IReadOnlyList<CatalogItem<SongAttributes>> ParseSongs(string body, string marketplace)
        => ParseSearch(body, marketplace, SearchKind.Song).Select(item => new CatalogItem<SongAttributes>(
            item.Id,
            marketplace,
            new SongAttributes
            {
                Name = item.Name,
                AlbumId = item.AlbumId,
                AlbumName = item.SecondaryText,
                Artists = string.IsNullOrWhiteSpace(item.SecondaryText) ? [] : [new CatalogArtist(null, item.SecondaryText)],
                Artwork = item.Artwork,
            })).ToList();

    /// <summary>Parses album search results.</summary>
    /// <param name="body">Raw response body.</param>
    /// <param name="marketplace">Source marketplace.</param>
    /// <returns>Parsed albums.</returns>
    internal static IReadOnlyList<CatalogItem<AlbumAttributes>> ParseAlbums(string body, string marketplace)
        => ParseSearch(body, marketplace, SearchKind.Album).Select(item => new CatalogItem<AlbumAttributes>(
            item.Id,
            marketplace,
            new AlbumAttributes
            {
                Name = item.Name,
                Artists = string.IsNullOrWhiteSpace(item.SecondaryText) ? [] : [new CatalogArtist(null, item.SecondaryText)],
                Artwork = item.Artwork,
            })).ToList();

    /// <summary>Parses artist search results.</summary>
    /// <param name="body">Raw response body.</param>
    /// <param name="marketplace">Source marketplace.</param>
    /// <returns>Parsed artists.</returns>
    internal static IReadOnlyList<CatalogItem<ArtistAttributes>> ParseArtists(string body, string marketplace)
        => ParseSearch(body, marketplace, SearchKind.Artist).Select(item => new CatalogItem<ArtistAttributes>(
            item.Id,
            marketplace,
            new ArtistAttributes { Name = item.Name, Artwork = item.Artwork })).ToList();

    /// <summary>Parses one track lookup.</summary>
    /// <param name="body">Raw response body.</param>
    /// <param name="marketplace">Source marketplace.</param>
    /// <returns>The song, or null.</returns>
    internal static CatalogItem<SongAttributes>? ParseSong(string body, string marketplace)
    {
        using var document = ParseGraphQl(body);
        var resource = ReadResource(document.RootElement, "track");
        if (resource is null)
        {
            return null;
        }

        var element = resource.Value;
        var id = RequiredString(element, "id");
        var album = element.TryGetProperty("album", out var albumElement) && albumElement.ValueKind == JsonValueKind.Object
            ? albumElement
            : default;
        var artists = ReadArtists(element);
        return new CatalogItem<SongAttributes>(id, marketplace, new SongAttributes
        {
            Name = RequiredString(element, "title"),
            AlbumId = album.ValueKind == JsonValueKind.Object ? OptionalString(album, "id") : null,
            AlbumName = album.ValueKind == JsonValueKind.Object ? OptionalString(album, "title") : null,
            Artists = artists,
            ReleaseDate = ReadDate(element),
            Artwork = ReadArtwork(element),
        })
        {
            ArtistIds = artists.Where(artist => !string.IsNullOrWhiteSpace(artist.Id)).Select(artist => artist.Id!).ToList(),
            AlbumIds = album.ValueKind == JsonValueKind.Object && OptionalString(album, "id") is { } albumId ? [albumId] : [],
        };
    }

    /// <summary>Parses one album lookup and its ordered tracks.</summary>
    /// <param name="metadataBody">Album metadata response.</param>
    /// <param name="tracksBody">Album tracks response.</param>
    /// <param name="marketplace">Source marketplace.</param>
    /// <returns>The album, or null.</returns>
    internal static CatalogItem<AlbumAttributes>? ParseAlbum(string metadataBody, string tracksBody, string marketplace)
    {
        using var metadata = ParseGraphQl(metadataBody);
        var resource = ReadResource(metadata.RootElement, "album");
        if (resource is null)
        {
            return null;
        }

        using var tracks = ParseGraphQl(tracksBody);
        var tracksResource = ReadResource(tracks.RootElement, "album");
        if (tracksResource is null)
        {
            throw new CatalogProtocolException("The album tracks response did not contain an album.");
        }

        var element = resource.Value;
        var artists = ReadArtists(element);
        var trackIds = tracksResource.Value.TryGetProperty("tracks", out var trackArray) && trackArray.ValueKind == JsonValueKind.Array
            ? trackArray.EnumerateArray().Select(track => RequiredString(track, "id")).ToList()
            : throw new CatalogProtocolException("The album tracks response did not contain a track array.");
        return new CatalogItem<AlbumAttributes>(RequiredString(element, "id"), marketplace, new AlbumAttributes
        {
            Name = RequiredString(element, "title"),
            Artists = artists,
            ReleaseDate = ReadDate(element),
            Copyright = OptionalString(element, "copyright"),
            TrackCount = element.TryGetProperty("trackCount", out var count) && count.TryGetInt32(out var value) ? value : null,
            Artwork = ReadArtwork(element),
        })
        {
            ArtistIds = artists.Where(artist => !string.IsNullOrWhiteSpace(artist.Id)).Select(artist => artist.Id!).ToList(),
            TrackIds = trackIds,
        };
    }

    /// <summary>Parses one artist lookup.</summary>
    /// <param name="body">Raw response body.</param>
    /// <param name="marketplace">Source marketplace.</param>
    /// <returns>The artist, or null.</returns>
    internal static CatalogItem<ArtistAttributes>? ParseArtist(string body, string marketplace)
    {
        using var document = ParseGraphQl(body);
        var resource = ReadResource(document.RootElement, "artist");
        return resource is null
            ? null
            : new CatalogItem<ArtistAttributes>(
                RequiredString(resource.Value, "id"),
                marketplace,
                new ArtistAttributes
                {
                    Name = RequiredString(resource.Value, "name"),
                    Artwork = ReadArtwork(resource.Value),
                });
    }

    private static JsonDocument ParseGraphQl(string body)
    {
        var document = Parse(body);
        var root = document.RootElement;
        if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
        {
            document.Dispose();
            throw new CatalogProtocolException("Amazon Music returned GraphQL errors.");
        }

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            document.Dispose();
            throw new CatalogProtocolException("Amazon Music returned no GraphQL data object.");
        }

        return document;
    }

    private static JsonElement? ReadResource(JsonElement root, string name)
    {
        var resource = root.GetProperty("data").GetProperty(name);
        return resource.ValueKind == JsonValueKind.Null
            ? null
            : resource.ValueKind == JsonValueKind.Object
                ? resource
                : throw new CatalogProtocolException($"The GraphQL {name} field was not an object.");
    }

    private static IReadOnlyList<SearchItem> ParseSearch(string body, string marketplace, SearchKind kind)
    {
        using var document = Parse(body);
        if (!document.RootElement.TryGetProperty("methods", out var methods) || methods.ValueKind != JsonValueKind.Array)
        {
            throw new CatalogProtocolException("The search response did not contain methods.");
        }

        var output = new List<SearchItem>();
        foreach (var method in methods.EnumerateArray())
        {
            if (!method.TryGetProperty("template", out var template)
                || !template.TryGetProperty("widgets", out var widgets)
                || widgets.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var widget in widgets.EnumerateArray())
            {
                if (!widget.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var item in items.EnumerateArray())
                {
                    var link = ReadDeeplink(item);
                    if (link is null || !TryReadIds(link, kind, out var id, out var albumId))
                    {
                        continue;
                    }

                    output.Add(new SearchItem(
                        id,
                        ReadText(item, "primaryText") ?? throw new CatalogProtocolException("A search item had no primary text."),
                        ReadText(item, "secondaryText"),
                        albumId,
                        ReadArtwork(item)));
                }
            }
        }

        return output.DistinctBy(item => item.Id).ToList();
    }

    private static JsonDocument Parse(string body)
    {
        try
        {
            return JsonDocument.Parse(body);
        }
        catch (JsonException ex)
        {
            throw new CatalogProtocolException($"Amazon Music returned malformed JSON ({ex.BytePositionInLine}).");
        }
    }

    private static string? ReadDeeplink(JsonElement item)
        => item.TryGetProperty("primaryLink", out var link) && link.ValueKind == JsonValueKind.Object
            ? OptionalString(link, "deeplink")
            : null;

    private static bool TryReadIds(string deeplink, SearchKind kind, out string id, out string? albumId)
    {
        id = string.Empty;
        albumId = null;
        if (!Uri.TryCreate("https://music.amazon.example" + deeplink, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var expected = kind switch
        {
            SearchKind.Song => "albums",
            SearchKind.Album => "albums",
            _ => "artists",
        };
        if (parts.Length < 2 || !string.Equals(parts[0], expected, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (kind == SearchKind.Song)
        {
            albumId = parts[1];
            var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
            const string key = "trackAsin=";
            var parameter = query.FirstOrDefault(value => value.StartsWith(key, StringComparison.OrdinalIgnoreCase));
            if (parameter is null)
            {
                return false;
            }

            id = Uri.UnescapeDataString(parameter[key.Length..]);
            return !string.IsNullOrWhiteSpace(id);
        }

        id = parts[1];
        return !string.IsNullOrWhiteSpace(id);
    }

    private static string? ReadText(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.ValueKind == JsonValueKind.Object
                ? OptionalString(value, "text")
                : null;
    }

    private static IReadOnlyList<CatalogArtist> ReadArtists(JsonElement element)
    {
        if (!element.TryGetProperty("contributingArtists", out var contributing)
            || !contributing.TryGetProperty("edges", out var edges)
            || edges.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return edges.EnumerateArray().Select(edge =>
        {
            var node = edge.GetProperty("node");
            return new CatalogArtist(OptionalString(node, "id"), RequiredString(node, "name"), OptionalString(edge, "role"));
        }).ToList();
    }

    private static Artwork? ReadArtwork(JsonElement element)
    {
        if (element.TryGetProperty("image", out var image) && image.ValueKind == JsonValueKind.String && image.GetString() is { } searchUrl)
        {
            return CreateArtwork(searchUrl, null, null);
        }

        if (!element.TryGetProperty("images", out var images) || images.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var candidates = images.EnumerateArray()
            .Select(value => new
            {
                Url = OptionalString(value, "url"),
                Width = value.TryGetProperty("width", out var width) && width.TryGetInt32(out var widthValue) ? widthValue : (int?)null,
                Height = value.TryGetProperty("height", out var height) && height.TryGetInt32(out var heightValue) ? heightValue : (int?)null,
            })
            .Where(value => value.Url is not null)
            .OrderByDescending(value => (value.Width ?? 0) * (value.Height ?? 0))
            .FirstOrDefault();
        return candidates is null ? null : CreateArtwork(candidates.Url!, candidates.Width, candidates.Height);
    }

    private static Artwork? CreateArtwork(string url, int? width, int? height)
        => Uri.TryCreate(url, UriKind.Absolute, out var parsed) && parsed.Scheme == Uri.UriSchemeHttps
            ? new Artwork(url, width, height)
            : null;

    private static DateTimeOffset? ReadDate(JsonElement element)
        => OptionalString(element, "releaseDate") is { } value
            && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
                ? date
                : null;

    private static string RequiredString(JsonElement element, string name)
        => OptionalString(element, name) ?? throw new CatalogProtocolException($"A required {name} field was missing.");

    private static string? OptionalString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private sealed record SearchItem(string Id, string Name, string? SecondaryText, string? AlbumId, Artwork? Artwork);
}
