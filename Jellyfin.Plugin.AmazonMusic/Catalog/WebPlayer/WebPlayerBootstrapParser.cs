using System;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.AmazonMusic.Catalog.WebPlayer;

/// <summary>
/// Parses the small set of runtime values exposed by the public web player.
/// </summary>
public static partial class WebPlayerBootstrapParser
{
    private static readonly Uri _defaultGraphQlEndpoint = new("https://gql.music.amazon.dev");

    /// <summary>
    /// Parses the guest runtime configuration.
    /// </summary>
    /// <param name="json">Configuration JSON.</param>
    /// <param name="marketplace">Marketplace definition.</param>
    /// <returns>The values needed to fetch and complete the bootstrap.</returns>
    public static WebPlayerConfig ParseConfig(string json, MarketplaceDefinition marketplace)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var bundle = RequiredString(root, "dragonflyBundle");
        var version = RequiredString(root, "version");
        var deviceType = RequiredString(root, "deviceType");
        var countryDomain = root.TryGetProperty("isDragonflyFFCountryDomainEnabled", out var flag)
            && flag.ValueKind == JsonValueKind.True;

        return new WebPlayerConfig(
            new Uri(marketplace.WebPlayerOrigin, bundle),
            version,
            deviceType,
            countryDomain ? marketplace.CountryGraphQlEndpoint : _defaultGraphQlEndpoint);
    }

    /// <summary>
    /// Extracts the anonymous application identifier from a web player bundle.
    /// </summary>
    /// <param name="javascript">Dragonfly JavaScript bundle.</param>
    /// <returns>The anonymous application identifier.</returns>
    public static string ExtractAnonymousApiKey(string javascript)
    {
        var match = AnonymousApiKeyRegex().Match(javascript);
        if (!match.Success)
        {
            throw new InvalidOperationException("Could not locate the anonymous web player application identifier.");
        }

        return match.Groups[1].Value;
    }

    private static string RequiredString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var property)
            || property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new InvalidOperationException($"The web player configuration did not contain {name}.");
        }

        return property.GetString()!;
    }

    [GeneratedRegex(@"FIREFLY_ANONYMOUS_WEB_API_KEY\s*(?::|=)\s*[\""']([^\""']+)[\""']", RegexOptions.CultureInvariant | RegexOptions.RightToLeft)]
    private static partial Regex AnonymousApiKeyRegex();
}
