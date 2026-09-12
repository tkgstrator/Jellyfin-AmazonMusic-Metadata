using System;

namespace Jellyfin.Plugin.AmazonMusic.Catalog.WebPlayer;

/// <summary>
/// Describes the public web player and catalog context for a marketplace.
/// </summary>
public sealed class MarketplaceDefinition
{
    private MarketplaceDefinition(
        string id,
        string territory,
        string locale,
        string currency,
        string timeZone,
        Uri webPlayerOrigin,
        Uri countryGraphQlEndpoint)
    {
        Id = id;
        Territory = territory;
        Locale = locale;
        Currency = currency;
        TimeZone = timeZone;
        WebPlayerOrigin = webPlayerOrigin;
        CountryGraphQlEndpoint = countryGraphQlEndpoint;
    }

    /// <summary>
    /// Gets the stable plugin marketplace identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the Amazon Music territory code.
    /// </summary>
    public string Territory { get; }

    /// <summary>
    /// Gets the default Amazon Music locale.
    /// </summary>
    public string Locale { get; }

    /// <summary>
    /// Gets the marketplace currency code.
    /// </summary>
    public string Currency { get; }

    /// <summary>
    /// Gets the marketplace time zone.
    /// </summary>
    public string TimeZone { get; }

    /// <summary>
    /// Gets the public web player origin.
    /// </summary>
    public Uri WebPlayerOrigin { get; }

    /// <summary>
    /// Gets the marketplace-specific GraphQL endpoint.
    /// </summary>
    public Uri CountryGraphQlEndpoint { get; }

    /// <summary>
    /// Resolves a configured marketplace identifier.
    /// </summary>
    /// <param name="marketplace">Plugin marketplace identifier.</param>
    /// <returns>The marketplace definition.</returns>
    public static MarketplaceDefinition Resolve(string marketplace)
        => marketplace.ToLowerInvariant() switch
        {
            CatalogOptions.Japan => new(
                CatalogOptions.Japan,
                "JP",
                "ja_JP",
                "JPY",
                "Asia/Tokyo",
                new Uri("https://music.amazon.co.jp"),
                new Uri("https://gql.music.amazon.co.jp")),
            CatalogOptions.UnitedStates => new(
                CatalogOptions.UnitedStates,
                "US",
                "en_US",
                "USD",
                "America/Los_Angeles",
                new Uri("https://music.amazon.com"),
                new Uri("https://gql.music.amazon.com")),
            _ => throw new ArgumentOutOfRangeException(nameof(marketplace), marketplace, "Unsupported Amazon Music marketplace."),
        };
}
