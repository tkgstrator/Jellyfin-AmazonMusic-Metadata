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
    /// <summary>
    /// Builds an Apollo GraphQL request with a stable logical cache identity.
    /// </summary>
    /// <param name="marketplace">Plugin marketplace identifier.</param>
    /// <param name="operationName">GraphQL operation name.</param>
    /// <param name="query">GraphQL query document.</param>
    /// <param name="variables">Operation variables.</param>
    /// <param name="kind">Request kind.</param>
    /// <param name="rootName">Expected field in the GraphQL data object.</param>
    /// <returns>The catalog request.</returns>
    public static CatalogRequest Build(
        string marketplace,
        string operationName,
        string query,
        object variables,
        CatalogRequestKind kind,
        string rootName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentNullException.ThrowIfNull(variables);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootName);

        var body = JsonSerializer.Serialize(
            new GraphQlEnvelope(operationName, variables, query),
            CatalogJson.Options);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
        var cacheKey = $"graphql:v1:{marketplace}:{operationName}:{hash}";
        return new CatalogRequest(
            HttpMethod.Post,
            "/",
            body,
            cacheKey,
            kind,
            marketplace,
            body => CatalogResponseValidator.ValidateGraphQl(body, rootName));
    }

    private sealed record GraphQlEnvelope(string OperationName, object Variables, string Query);
}
