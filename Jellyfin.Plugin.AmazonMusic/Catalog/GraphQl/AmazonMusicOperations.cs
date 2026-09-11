namespace Jellyfin.Plugin.AmazonMusic.Catalog.GraphQl;

/// <summary>
/// Contains the minimal GraphQL documents used for anonymous catalog metadata.
/// </summary>
public static class AmazonMusicOperations
{
    /// <summary>
    /// Gets the cross-entity text search document.
    /// </summary>
    public const string TenzingTextSearch = """
        query tenzingTextSearch($textSearchRequest: TenzingTextSearchGqlRequest) {
          tenzingTextSearch(body: $textSearchRequest) {
            result {
              metricId edgeCount totalCount label
              edges { node {
                __typename
                ... on Track { id title shortTitle releaseDate images { url width height } }
                ... on Album { id title releaseDate images { url width height } }
                ... on Artist { id name images { url width height imageType } }
              } }
            }
          }
        }
        """;

    /// <summary>
    /// Gets the track metadata document.
    /// </summary>
    public const string TrackMetadata = """
        query trackMetadata($id: String!) {
          track(id: $id) {
            id title shortTitle releaseDate languageOfPerformance
            images { url width height }
            album { id title images { url width height } }
            contributingArtists { edges { node { id name } role } }
          }
        }
        """;

    /// <summary>
    /// Gets the album metadata document.
    /// </summary>
    public const string GetAlbumMetadata = """
        query getAlbumMetadata($id: String!) {
          album(id: $id) {
            id title copyright trackCount duration releaseDate format
            images { url width height }
            contributingArtists { edges { node { id name } role } }
          }
        }
        """;

    /// <summary>
    /// Gets the ordered album tracks document.
    /// </summary>
    public const string GetAlbumTracks = """
        query getAlbumTracks($id: String!) {
          album(id: $id) {
            id tracks { edgeCount edges { node { id title } } }
          }
        }
        """;

    /// <summary>
    /// Gets the artist summary document.
    /// </summary>
    public const string GetArtistSummary = """
        query getArtistSummary($artistId: String!) {
          artist(id: $artistId) {
            id name followerCount biography { text }
            images { width height url imageType }
            tracks { edgeCount }
          }
        }
        """;
}
