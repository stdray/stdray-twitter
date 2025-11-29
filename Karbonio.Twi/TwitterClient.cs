using System.Net.Http.Headers;
using System.Text.Json;

namespace Karbonio.Twi;

public record Tweet(string Id, string Text, Media[] Media);

public record Media(string Type, string Url);

public class TwitterClient(HttpClient httpClient)
{
    private static readonly string BearerToken = Uri.UnescapeDataString("AAAAAAAAAAAAAAAAAAAAANRILgAAAAAAnNwIzUejRCOuH5E6I8xnZz4puTs%3D1Zv7ttfk8LF81IUq16cHjhLTvJu4FA33AGWWjCpTnA");
    private const string GraphQLEndpoint = "https://x.com/i/api/graphql/2ICDjqPd81tulZcYrtpTuQ/TweetResultByRestId";
    private const string GuestTokenEndpoint = "https://api.x.com/1.1/guest/activate.json";

    public async Task<Tweet> GetTweetByIdAsync(string tweetId)
    {
        var query = BuildGraphQLQuery(tweetId);
        var json = await CallGraphQLApiAsync(GraphQLEndpoint, tweetId, query);
        return ParseTweet(json, tweetId);
    }

    private async Task<string> CallGraphQLApiAsync(string endpoint, string tweetId, Dictionary<string, string> query)
    {
        var headers = SetBaseHeaders();
        var guestToken = await FetchGuestTokenAsync();
        headers["x-guest-token"] = guestToken;
        headers["x-twitter-client-language"] = "en";
        headers["x-twitter-active-user"] = "yes";
        
        var queryString = string.Join("&", query.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
        var url = $"{endpoint}?{queryString}";
        
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", BearerToken);
        foreach (var header in headers)
        {
            request.Headers.Add(header.Key, header.Value);
        }
        request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        request.Headers.Add("Accept", "application/json, text/plain, */*");
        request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
        request.Headers.Add("Referer", "https://x.com/");
        request.Headers.Add("Origin", "https://x.com");
        
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

    private static Dictionary<string, string> SetBaseHeaders()
    {
        return new Dictionary<string, string>();
    }

    private async Task<string> FetchGuestTokenAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, GuestTokenEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", BearerToken);
        request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        request.Headers.Add("Accept", "application/json");
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

    private static Dictionary<string, string> BuildGraphQLQuery(string tweetId)
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

    private static Tweet ParseTweet(string json, string tweetId)
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
        
        var media = new List<Media>();
        
        if (legacy.TryGetProperty("extended_entities", out var extendedEntities) &&
            extendedEntities.TryGetProperty("media", out var mediaArray))
        {
            foreach (var item in mediaArray.EnumerateArray())
            {
                var type = item.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "" : "";
                string? url = null;
                
                if (type == "photo")
                {
                    if (item.TryGetProperty("media_url_https", out var photoUrl))
                    {
                        url = photoUrl.GetString();
                    }
                }
                else if (type == "video" || type == "animated_gif")
                {
                    if (item.TryGetProperty("video_info", out var videoInfo) &&
                        videoInfo.TryGetProperty("variants", out var variants))
                    {
                        var bestVariant = variants.EnumerateArray()
                            .Where(v => v.TryGetProperty("url", out _))
                            .OrderByDescending(v => 
                            {
                                if (v.TryGetProperty("bitrate", out var br))
                                {
                                    return br.TryGetInt32(out var bitrate) ? bitrate : 0;
                                }
                                return 0;
                            })
                            .FirstOrDefault();
                        
                        if (bestVariant.ValueKind != JsonValueKind.Undefined &&
                            bestVariant.TryGetProperty("url", out var videoUrl))
                        {
                            url = videoUrl.GetString();
                        }
                    }
                }
                
                if (url != null)
                {
                    media.Add(new Media(type, url));
                }
            }
        }
        
        return new Tweet(tweetId, text, media.ToArray());
    }
}

