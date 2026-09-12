using System.Text.Json;
using Jellyfin.Plugin.AmazonMusic.Catalog.Parsing;

namespace Jellyfin.Plugin.AmazonMusic.Catalog;

/// <summary>
/// Rejects protocol failures before raw responses enter the cache.
/// </summary>
internal static class CatalogResponseValidator
{
    /// <summary>
    /// Validates a GraphQL response envelope.
    /// </summary>
    /// <param name="body">Raw response body.</param>
    public static void ValidateGraphQl(string body)
    {
        using var document = Parse(body);
        var root = document.RootElement;
        if (root.TryGetProperty("errors", out var errors)
            && errors.ValueKind == JsonValueKind.Array
            && errors.GetArrayLength() > 0)
        {
            throw new CatalogProtocolException("Amazon Music returned GraphQL errors.");
        }

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            throw new CatalogProtocolException("Amazon Music returned no GraphQL data object.");
        }
    }

    /// <summary>
    /// Validates a Web Player search response envelope.
    /// </summary>
    /// <param name="body">Raw response body.</param>
    public static void ValidateSearch(string body)
    {
        using var document = Parse(body);
        if (!document.RootElement.TryGetProperty("methods", out var methods)
            || methods.ValueKind != JsonValueKind.Array)
        {
            throw new CatalogProtocolException("Amazon Music returned no search methods array.");
        }
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
}
