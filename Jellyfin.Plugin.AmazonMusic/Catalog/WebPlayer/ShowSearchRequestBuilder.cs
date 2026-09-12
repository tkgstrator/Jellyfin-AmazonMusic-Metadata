using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Jellyfin.Plugin.AmazonMusic.Catalog.WebPlayer;

/// <summary>
/// Builds logical requests for the desktop Web Player search backend.
/// </summary>
public static class ShowSearchRequestBuilder
{
    internal const string UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

    /// <summary>
    /// Builds a search request without embedding transient session values in its cache identity.
    /// </summary>
    /// <param name="marketplace">Plugin marketplace identifier.</param>
    /// <param name="keyword">Search keyword.</param>
    /// <returns>The logical search request.</returns>
    public static CatalogRequest Build(string marketplace, string keyword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyword);
        var body = JsonSerializer.Serialize(new SearchInput(keyword), CatalogJson.Options);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{marketplace}\n{keyword}"))).ToLowerInvariant();
        return new CatalogRequest(
            HttpMethod.Post,
            "/api/showSearch",
            body,
            $"show-search:v1:{marketplace}:{hash}",
            CatalogRequestKind.Search,
            marketplace,
            CatalogResponseValidator.ValidateSearch);
    }

    internal static string BuildWireBody(
        string keyword,
        WebPlayerBootstrap bootstrap,
        string requestId,
        long timestamp)
    {
        var authentication = JsonSerializer.Serialize(
            new
            {
                @interface = "ClientAuthenticationInterface.v1_0.ClientTokenElement",
                accessToken = string.Empty,
            },
            CatalogJson.Options);
        var csrf = JsonSerializer.Serialize(
            new
            {
                @interface = "CSRFInterface.v1_0.CSRFHeaderElement",
                token = bootstrap.Csrf.Token,
                timestamp = bootstrap.Csrf.Timestamp,
                rndNonce = bootstrap.Csrf.RandomNonce,
            },
            CatalogJson.Options);
        var origin = bootstrap.Marketplace.WebPlayerOrigin;
        var domain = origin.Host;
        var headers = JsonSerializer.Serialize(
            new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["x-amzn-authentication"] = authentication,
                ["x-amzn-device-model"] = "WEBPLAYER",
                ["x-amzn-device-width"] = "1920",
                ["x-amzn-device-height"] = "1080",
                ["x-amzn-device-family"] = "WebPlayer",
                ["x-amzn-device-id"] = bootstrap.DeviceId,
                ["x-amzn-user-agent"] = UserAgent,
                ["x-amzn-session-id"] = bootstrap.SessionId,
                ["x-amzn-request-id"] = requestId,
                ["x-amzn-device-language"] = bootstrap.Marketplace.Locale,
                ["x-amzn-currency-of-preference"] = bootstrap.Marketplace.Currency,
                ["x-amzn-os-version"] = "1.0",
                ["x-amzn-application-version"] = bootstrap.AppVersion,
                ["x-amzn-device-time-zone"] = bootstrap.Marketplace.TimeZone,
                ["x-amzn-timestamp"] = timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["x-amzn-csrf"] = csrf,
                ["x-amzn-music-domain"] = domain,
                ["x-amzn-referer"] = domain,
                ["x-amzn-affiliate-tags"] = string.Empty,
                ["x-amzn-ref-marker"] = string.Empty,
                ["x-amzn-page-url"] = new Uri(origin, "/search").AbsoluteUri,
                ["x-amzn-weblab-id-overrides"] = string.Empty,
                ["x-amzn-video-player-token"] = string.Empty,
                ["x-amzn-feature-flags"] = "hd-supported,uhd-supported",
                ["x-amzn-has-profile-id"] = string.Empty,
                ["x-amzn-age-band"] = string.Empty,
            },
            CatalogJson.Options);
        var keywordPayload = JsonSerializer.Serialize(
            new
            {
                @interface = "Web.TemplatesInterface.v1_0.Touch.SearchTemplateInterface.SearchKeywordClientInformation",
                keyword,
            },
            CatalogJson.Options);
        var userHash = JsonSerializer.Serialize(new { level = "LIBRARY_MEMBER" }, CatalogJson.Options);

        return JsonSerializer.Serialize(
            new { keyword = keywordPayload, userHash, headers },
            CatalogJson.Options);
    }

    internal static string ReadKeyword(CatalogRequest request)
    {
        using var document = JsonDocument.Parse(request.Body!);
        return document.RootElement.GetProperty("keyword").GetString()
            ?? throw new InvalidOperationException("The search request did not contain a keyword.");
    }

    private sealed record SearchInput(string Keyword);
}
