using System.Text.Json;
using Jellyfin.Plugin.AmazonMusic.Catalog.Parsing;

namespace Jellyfin.Plugin.AmazonMusic.Catalog;

/// <summary>
/// Rejects protocol failures before raw responses enter the cache.
/// </summary>
internal static class CatalogResponseValidator
{
    /// <summary>
    /// Validates a GraphQL response envelope and operation root.
    /// </summary>
    /// <param name="body">Raw response body.</param>
    /// <param name="rootName">Expected field in the GraphQL data object.</param>
    public static void ValidateGraphQl(string body, string rootName)
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

        if (!data.TryGetProperty(rootName, out var operationRoot)
            || operationRoot.ValueKind is not (JsonValueKind.Object or JsonValueKind.Null))
        {
            throw new CatalogProtocolException($"Amazon Music returned no valid GraphQL {rootName} field.");
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

        foreach (var method in methods.EnumerateArray())
        {
            if (method.ValueKind != JsonValueKind.Object
                || !method.TryGetProperty("template", out var template)
                || template.ValueKind != JsonValueKind.Object
                || !template.TryGetProperty("widgets", out var widgets)
                || widgets.ValueKind != JsonValueKind.Array)
            {
                throw new CatalogProtocolException("Amazon Music returned an invalid search method.");
            }

            foreach (var widget in widgets.EnumerateArray())
            {
                if (widget.ValueKind != JsonValueKind.Object
                    || !widget.TryGetProperty("items", out var items)
                    || items.ValueKind != JsonValueKind.Array)
                {
                    throw new CatalogProtocolException("Amazon Music returned an invalid search widget.");
                }
            }
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
