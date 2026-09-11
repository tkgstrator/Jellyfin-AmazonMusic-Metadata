using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Jellyfin.Plugin.AmazonMusic.Catalog.GraphQl;

/// <summary>
/// Builds anonymous Amazon Music GraphQL catalog requests.
/// </summary>
public static class GraphQlRequestBuilder
{
    private static readonly string[] _albumFields = [
        "asin", "artOriginal", "title", "artistAsin", "artistName", "originalReleaseDate"];

    private static readonly string[] _artistFields = ["asin", "name", "artOriginal"];

    private static readonly string[] _trackFields = [
        "asin", "title", "artistAsin", "artistName", "artOriginal", "artist",
        "parentalControls.hasExplicitLanguage", "originalReleaseDate", "languageOfPerformance"];

    /// <summary>
    /// Builds the catalog search used by the desktop Web Player.
    /// </summary>
    /// <param name="marketplace">Plugin marketplace identifier.</param>
    /// <param name="query">Search text.</param>
    /// <param name="deviceId">Anonymous device identifier.</param>
    /// <param name="deviceType">Web Player device type.</param>
    /// <param name="locale">Amazon Music locale.</param>
    /// <param name="territory">Amazon Music territory.</param>
    /// <param name="limit">Maximum results requested for each entity type.</param>
    /// <returns>The catalog request.</returns>
    public static CatalogRequest BuildSearch(
        string marketplace,
        string query,
        string deviceId,
        string deviceType,
        string locale,
        string territory,
        int limit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        var resultLimit = Math.Clamp(limit, 1, 100);
        var restrictions = new
        {
            allowedParentalControls = new { hasExplicitLanguage = true },
            assetQuality = new { quality = Array.Empty<string>() },
            contentTier = (string?)null,
        };
        var textSearchRequest = new
        {
            customerIdentity = new { deviceId, deviceType },
            features = new
            {
                spellCorrection = new { allowCorrection = true },
                upsell = new { allowUpsellForCatalogContent = true },
            },
            locale,
            musicTerritory = territory,
            query,
            resultSpecs = new object[]
            {
                new
                {
                    documentSpecs = new[] { new { type = "catalog_album", fields = _albumFields } },
                    label = "Albums",
                    maxResults = resultLimit,
                    contentRestrictions = restrictions,
                },
                new
                {
                    documentSpecs = new[] { new { type = "catalog_artist", fields = _artistFields } },
                    label = "Artists",
                    maxResults = resultLimit,
                    contentRestrictions = restrictions,
                },
                new
                {
                    documentSpecs = new[] { new { type = "catalog_track", fields = _trackFields } },
                    label = "Songs",
                    maxResults = resultLimit,
                    contentRestrictions = restrictions,
                },
            },
        };

        return Build(
            marketplace,
            "tenzingTextSearch",
            AmazonMusicOperations.TenzingTextSearch,
            new { textSearchRequest },
            CatalogRequestKind.Search);
    }

    /// <summary>
    /// Builds an Apollo GraphQL request with a stable logical cache identity.
    /// </summary>
    /// <param name="marketplace">Plugin marketplace identifier.</param>
    /// <param name="operationName">GraphQL operation name.</param>
    /// <param name="query">GraphQL query document.</param>
    /// <param name="variables">Operation variables.</param>
    /// <param name="kind">Request kind.</param>
    /// <returns>The catalog request.</returns>
    public static CatalogRequest Build(
        string marketplace,
        string operationName,
        string query,
        object variables,
        CatalogRequestKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentNullException.ThrowIfNull(variables);

        var body = JsonSerializer.Serialize(
            new GraphQlEnvelope(operationName, variables, query),
            CatalogJson.Options);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
        var cacheKey = $"graphql:v1:{marketplace}:{operationName}:{hash}";
        return new CatalogRequest(HttpMethod.Post, "/", body, cacheKey, kind, marketplace);
    }

    private sealed record GraphQlEnvelope(string OperationName, object Variables, string Query);
}
