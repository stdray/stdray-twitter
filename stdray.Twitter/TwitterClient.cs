using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace stdray.Twitter;

/// <summary>
/// Represents the type of media in a tweet.
/// </summary>
public enum MediaType
{
    /// <summary>
    /// A photo media type.
    /// </summary>
    Photo,

    /// <summary>
    /// A video media type.
    /// </summary>
    Video,

    /// <summary>
    /// An animated GIF media type.
    /// </summary>
    AnimatedGif
}

/// <summary>
/// Represents a video variant with its URL, bitrate, and dimensions.
/// </summary>
/// <param name="Url">The URL of the video variant.</param>
/// <param name="Bitrate">The bitrate of the video in bits per second.</param>
/// <param name="Width">The width of the video in pixels.</param>
/// <param name="Height">The height of the video in pixels.</param>
public record VideoVariant(string Url, int? Bitrate, int? Width, int? Height);

/// <summary>
/// Represents a tweet with its ID, text content, and associated media.
/// </summary>
/// <param name="Id">The unique identifier of the tweet.</param>
/// <param name="Text">The text content of the tweet.</param>
/// <param name="Media">An array of media objects associated with the tweet.</param>
public record Tweet(string Id, string Text, Media[] Media);

/// <summary>
/// Represents media content in a tweet.
/// </summary>
/// <param name="Type">The type of media (Photo, Video, or AnimatedGif).</param>
/// <param name="Url">The URL of the media content, if applicable.</param>
/// <param name="Variants">An array of video variants, if the media is a video or animated GIF.</param>
public record Media(MediaType Type, string? Url, VideoVariant[]? Variants);

/// <summary>
/// A client for retrieving tweet content from Twitter/X.com by ID.
/// </summary>
public class TwitterClient(HttpClient httpClient)
{
    const string EncodedBearerToken = "AAAAAAAAAAAAAAAAAAAAANRILgAAAAAAnNwIzUejRCOuH5E6I8xnZz4puTs%3D1Zv7ttfk8LF81IUq16cHjhLTvJu4FA33AGWWjCpTnA";
    static readonly string BearerToken = Uri.UnescapeDataString(EncodedBearerToken);
    const string GraphQlEndpoint = "https://x.com/i/api/graphql/2ICDjqPd81tulZcYrtpTuQ/TweetResultByRestId";
    const string GuestTokenEndpoint = "https://api.x.com/1.1/guest/activate.json";

    /// <summary>
    /// Retrieves a tweet by its ID.
    /// </summary>
    /// <param name="tweetId">The unique identifier of the tweet to retrieve.</param>
    /// <returns>A <see cref="Tweet"/> object containing the tweet's ID, text, and media.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the Twitter API returns an error or the tweet is unavailable.</exception>
    public async Task<Tweet> GetTweetById(string tweetId)
    {
        var query = BuildGraphQlQuery(tweetId);
        var json = await CallGraphQl(GraphQlEndpoint, tweetId, query);
        return ParseTweet(json, tweetId);
    }

    async Task<string> CallGraphQl(string endpoint, string tweetId, Dictionary<string, string> query)
    {
        var guestToken = await FetchGuestToken();

        var queryString = string.Join("&", query.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
        var url = $"{endpoint}?{queryString}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", BearerToken);
        request.Headers.Add("x-guest-token", guestToken);
        AddConstantHeaders(request);

        var response = await httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            var errorMsg = errorContent.Length > 500 ? errorContent.Substring(0, 500) + "..." : errorContent;
            throw new HttpRequestException($"HTTP {response.StatusCode}: {errorMsg}");
        }

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("errors", out var errors))
        {
            var errorMessages = errors.EnumerateArray()
                .Select(e => e.TryGetProperty("message", out var msg) ? msg.GetString() : "Unknown error")
                .Where(m => m != null)
                .Distinct()
                .ToList();

            var errorText = string.Join(", ", errorMessages);
            if (errorText.Contains("not authorized", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Not authorized: {errorText}");
            }
            throw new InvalidOperationException($"Twitter API error: {errorText}");
        }

        return json;
    }

    static void AddConstantHeaders(HttpRequestMessage request)
    {
        request.Headers.Add("x-twitter-client-language", "en");
        request.Headers.Add("x-twitter-active-user", "yes");
        request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        request.Headers.Add("Accept", "application/json, text/plain, */*");
        request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
        request.Headers.Add("Referer", "https://x.com/");
        request.Headers.Add("Origin", "https://x.com");
    }

    async Task<string> FetchGuestToken()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, GuestTokenEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", BearerToken);
        AddConstantHeaders(request);
        request.Headers.Remove("Accept"); // Remove the default Accept header
        request.Headers.Add("Accept", "application/json");  // Add guest token specific Accept header
        request.Content = new StringContent("");

        var response = await httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Failed to get guest token: HTTP {response.StatusCode}: {errorContent}");
        }

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("guest_token").GetString()
            ?? throw new InvalidOperationException("Failed to get guest token");
    }

    static Dictionary<string, string> BuildGraphQlQuery(string tweetId)
    {
        var variables = JsonSerializer.Serialize(new
        {
            tweetId,
            withCommunity = false,
            includePromotedContent = false,
            withVoice = false
        });

        var features = JsonSerializer.Serialize(new
        {
            creator_subscriptions_tweet_preview_api_enabled = true,
            tweetypie_unmention_optimization_enabled = true,
            responsive_web_edit_tweet_api_enabled = true,
            graphql_is_translatable_rweb_tweet_is_translatable_enabled = true,
            view_counts_everywhere_api_enabled = true,
            longform_notetweets_consumption_enabled = true,
            responsive_web_twitter_article_tweet_consumption_enabled = false,
            tweet_awards_web_tipping_enabled = false,
            freedom_of_speech_not_reach_fetch_enabled = true,
            standardized_nudges_misinfo = true,
            tweet_with_visibility_results_prefer_gql_limited_actions_policy_enabled = true,
            longform_notetweets_rich_text_read_enabled = true,
            longform_notetweets_inline_media_enabled = true,
            responsive_web_graphql_exclude_directive_enabled = true,
            verified_phone_label_enabled = false,
            responsive_web_media_download_video_enabled = false,
            responsive_web_graphql_skip_user_profile_image_extensions_enabled = false,
            responsive_web_graphql_timeline_navigation_enabled = true,
            responsive_web_enhance_cards_enabled = false
        });

        var fieldToggles = JsonSerializer.Serialize(new { withArticleRichContentState = false });

        return new Dictionary<string, string>
        {
            ["variables"] = variables,
            ["features"] = features,
            ["fieldToggles"] = fieldToggles
        };
    }

    static Tweet ParseTweet(string json, string tweetId)
    {
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("errors", out var errors))
        {
            var errorMsg = errors.EnumerateArray()
                .Select(e => e.TryGetProperty("message", out var msg) ? msg.GetString() : "Unknown error")
                .FirstOrDefault() ?? "Unknown error";
            throw new InvalidOperationException($"Twitter API error: {errorMsg}");
        }

        var result = root.GetProperty("data").GetProperty("tweetResult").GetProperty("result");
        var typename = result.TryGetProperty("__typename", out var tn) ? tn.GetString() : "";

        if (typename == "TweetUnavailable" || typename == "TweetTombstone")
        {
            throw new InvalidOperationException("Tweet is unavailable");
        }

        if (typename == "TweetWithVisibilityResults")
        {
            result = result.GetProperty("tweet");
        }

        var legacy = result.GetProperty("legacy");

        var text = (legacy.TryGetProperty("full_text", out var ft) ? ft.GetString()
            : legacy.TryGetProperty("text", out var t) ? t.GetString()
            : null) ?? "";

        var media = ParseMedia(legacy);

        return new Tweet(tweetId, text, media.ToArray());
    }

    static List<Media> ParseMedia(JsonElement legacy)
    {
        var media = new List<Media>();

        if (legacy.TryGetProperty("extended_entities", out var extendedEntities) &&
            extendedEntities.TryGetProperty("media", out var mediaArray))
        {
            foreach (var item in mediaArray.EnumerateArray())
            {
                var typeStr = item.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "" : "";
                MediaType mediaType = typeStr switch
                {
                    "photo" => MediaType.Photo,
                    "video" => MediaType.Video,
                    "animated_gif" => MediaType.AnimatedGif,
                    _ => throw new InvalidOperationException($"Unknown media type: {typeStr}")
                };

                string? url = null;
                VideoVariant[]? variants = null;

                if (mediaType == MediaType.Photo)
                {
                    if (item.TryGetProperty("media_url_https", out var photoUrl))
                    {
                        url = photoUrl.GetString();
                    }
                }
                else if (mediaType is MediaType.Video or MediaType.AnimatedGif)
                {
                    if (item.TryGetProperty("video_info", out var videoInfo) &&
                        videoInfo.TryGetProperty("variants", out var variantsJson))
                    {
                        var variantList = new List<VideoVariant>();

                        foreach (var variant in variantsJson.EnumerateArray())
                        {
                            if (!variant.TryGetProperty("url", out var variantUrl))
                                continue;

                            var variantUrlStr = variantUrl.GetString();
                            if (variantUrlStr == null)
                                continue;

                            int? bitrate = null;
                            if (variant.TryGetProperty("bitrate", out var br) && br.TryGetInt32(out var bitrateVal))
                            {
                                bitrate = bitrateVal;
                            }

                            int? width = null, height = null;
                            if (variant.TryGetProperty("width", out var w) && w.TryGetInt32(out var widthVal))
                            {
                                width = widthVal;
                            }
                            if (variant.TryGetProperty("height", out var h) && h.TryGetInt32(out var heightVal))
                            {
                                height = heightVal;
                            }

                            if (width == null || height == null)
                            {
                                var dimensions = ExtractDimensionsFromUrl(variantUrlStr);
                                width ??= dimensions.width;
                                height ??= dimensions.height;
                            }

                            variantList.Add(new VideoVariant(variantUrlStr, bitrate, width, height));
                        }

                        variants = variantList.ToArray();

                        var bestVariant = variantList
                            .OrderByDescending(v => v.Bitrate ?? 0)
                            .FirstOrDefault();

                        url = bestVariant?.Url;
                    }
                }

                if (url != null || variants != null)
                {
                    media.Add(new Media(mediaType, url, variants));
                }
            }
        }

        return media;
    }

    static (int? width, int? height) ExtractDimensionsFromUrl(string url)
    {
        var match = Regex.Match(url, @"/(\d+)x(\d+)/");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var width) &&
            int.TryParse(match.Groups[2].Value, out var height))
        {
            return (width, height);
        }
        return (null, null);
    }
}

