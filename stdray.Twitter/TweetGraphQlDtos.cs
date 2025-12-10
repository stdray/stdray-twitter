using System.Text.Json.Serialization;

namespace stdray.Twitter;

internal record TweetResponseDto
{
    public TweetDataDto? Data { get; init; }

    public TweetErrorDto[]? Errors { get; init; }
}

internal record TweetDataDto
{
    public TweetResultDto? TweetResult { get; init; }
}

internal record TweetResultDto
{
    public TweetResultBodyDto? Result { get; init; }
}

internal record TweetResultBodyDto
{
    [JsonPropertyName("__typename")] public string? TypeName { get; init; }

    public TweetLegacyDto? Legacy { get; init; }

    public TweetResultBodyDto? Tweet { get; init; }
}

internal record TweetLegacyDto
{
    [JsonPropertyName("full_text")] public string? FullText { get; init; }

    public string? Text { get; init; }

    [JsonPropertyName("extended_entities")]
    public ExtendedEntitiesDto? ExtendedEntities { get; init; }
}

internal record ExtendedEntitiesDto
{
    public MediaEntityDto[]? Media { get; init; }
}

internal record MediaEntityDto
{
    public string? Type { get; init; }

    [JsonPropertyName("media_url_https")] public string? MediaUrlHttps { get; init; }

    [JsonPropertyName("video_info")] public VideoInfoDto? VideoInfo { get; init; }
}

internal record VideoInfoDto
{
    public VideoVariantDto[]? Variants { get; init; }
}

internal record VideoVariantDto
{
    public string? Url { get; init; }

    public int? Bitrate { get; init; }

    public int? Width { get; init; }

    public int? Height { get; init; }
}

internal record TweetErrorDto
{
    public string? Message { get; init; }
}
