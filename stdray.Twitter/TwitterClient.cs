using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace stdray.Twitter;

/// <summary>
///     A client for retrieving tweet content from Twitter/X.com by ID.
/// </summary>
public class TwitterClient(HttpClient httpClient)
{
    const string BearerToken =
        "AAAAAAAAAAAAAAAAAAAAANRILgAAAAAAnNwIzUejRCOuH5E6I8xnZz4puTs%3D1Zv7ttfk8LF81IUq16cHjhLTvJu4FA33AGWWjCpTnA";

    const string GraphQlEndpoint = "https://x.com/i/api/graphql/2ICDjqPd81tulZcYrtpTuQ/TweetResultByRestId";
    const string GuestTokenEndpoint = "https://api.x.com/1.1/guest/activate.json";

    /// <summary>
    ///     Retrieves a tweet by its ID.
    /// </summary>
    /// <param name="tweetId">The unique identifier of the tweet to retrieve.</param>
    /// <returns>A <see cref="Tweet" /> object containing the tweet's ID, text, and media.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the Twitter API returns an error or the tweet is unavailable.</exception>
    public async Task<Tweet> GetTweetById(string tweetId)
    {
        var query = BuildGraphQlQuery(tweetId);
        var json = await CallGraphQl(GraphQlEndpoint, query)
                   ?? throw new InvalidOperationException("Failed to parse Twitter response");
        return GetTweet(json, tweetId);
    }

    async Task<TweetResponseDto?> CallGraphQl(string endpoint, Dictionary<string, string> query)
    {
        var guestToken = await FetchGuestToken();

        var queryString = string.Join("&", query.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
        var url = $"{endpoint}?{queryString}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        SetDefaultHeaders(request);
        request.Headers.Add("x-guest-token", guestToken);

        var response = await httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            var errorMsg = errorContent.Length > 500 ? errorContent[..500] + "..." : errorContent;
            throw new HttpRequestException($"HTTP {response.StatusCode}: {errorMsg}");
        }

        return await response.Content.ReadFromJsonAsync(GraphQlContext.Default.TweetResponseDto);
    }

    static void SetDefaultHeaders(HttpRequestMessage request)
    {
        request.Headers.Remove("Accept");
        request.Headers.Authorization = new("Bearer", BearerToken);
        request.Headers.Add("x-twitter-client-language", "en");
        request.Headers.Add("x-twitter-active-user", "yes");
        request.Headers.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        request.Headers.Add("Accept", "application/json");
        request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
        request.Headers.Add("Referer", "https://x.com/");
        request.Headers.Add("Origin", "https://x.com");
    }

    async Task<string> FetchGuestToken()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, GuestTokenEndpoint);
        SetDefaultHeaders(request);
        // request.Content = new StringContent("");

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

        return new()
        {
            ["variables"] = variables,
            ["features"] = features,
            ["fieldToggles"] = fieldToggles
        };
    }

    static Tweet GetTweet(TweetResponseDto response, string tweetId)
    {
        if (response.Errors is { Length: > 0 })
        {
            var errorMessages = response.Errors
                .Select(e => e.Message)
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var errorText = errorMessages.Length > 0 ? string.Join(", ", errorMessages) : "Unknown error";
            if (errorText.Contains("not authorized", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Not authorized: {errorText}");

            throw new InvalidOperationException($"Twitter API error: {errorText}");
        }

        var resolvedTweet = ResolveTweet(response.Data?.TweetResult?.Result)
                            ?? throw new InvalidOperationException("Tweet is unavailable");

        var typeName = resolvedTweet.TypeName ?? string.Empty;
        if (typeName is "TweetUnavailable" or "TweetTombstone")
            throw new InvalidOperationException("Tweet is unavailable");

        var legacy = resolvedTweet.Legacy ??
                     throw new InvalidOperationException("Tweet payload is missing legacy data");
        var text = legacy.FullText ?? legacy.Text ?? string.Empty;
        var media = ParseMedia(legacy.ExtendedEntities?.Media).ToArray();

        return new(tweetId, text, media);
    }

    static TweetResultBodyDto? ResolveTweet(TweetResultBodyDto? result)
    {
        return result switch
        {
            null => null,
            { TypeName: "TweetWithVisibilityResults" } => result.Tweet,
            _ => result
        };
    }


    static IEnumerable<Media> ParseMedia(MediaEntityDto[]? mediaArray)
    {
        foreach (var item in mediaArray ?? [])
        {
            var mediaType = ParseType(item.Type);
            if (mediaType == MediaType.Photo && item.MediaUrlHttps is { } url)
            {
                yield return new PhotoMedia(url);
            }
            else if (mediaType is MediaType.Video or MediaType.AnimatedGif)
            {
                var variants = item.VideoInfo?.Variants
                    ?.Select(CreateVideoVariant)
                    .OfType<VideoVariant>()
                    .ToArray();
                if (variants?.Length > 0)
                    yield return new VideoMedia(mediaType, variants);
            }
        }

        yield break;

        static MediaType ParseType(string? typeStr)
        {
            return typeStr switch
            {
                "photo" => MediaType.Photo,
                "video" => MediaType.Video,
                "animated_gif" => MediaType.AnimatedGif,
                _ => throw new InvalidOperationException($"Unknown media type: {typeStr}")
            };
        }

        static VideoVariant? CreateVideoVariant(VideoVariantDto variant)
        {
            if (variant.Url is not { } variantUrlStr)
                return null;

            var bitrate = variant.Bitrate;
            var width = variant.Width;
            var height = variant.Height;

            if (width == null || height == null)
            {
                var dimensions = ExtractDimensionsFromUrl(variantUrlStr);
                width ??= dimensions.width;
                height ??= dimensions.height;
            }

            var videoVariant = new VideoVariant(variantUrlStr, bitrate, width, height);
            return videoVariant;
        }

        static (int? width, int? height) ExtractDimensionsFromUrl(string url)
        {
            var match = Regex.Match(url, @"/(\d+)x(\d+)/");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var width) &&
                int.TryParse(match.Groups[2].Value, out var height))
                return (width, height);

            return (null, null);
        }
    }
}