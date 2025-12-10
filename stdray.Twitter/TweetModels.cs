namespace stdray.Twitter;

/// <summary>
///     Represents the type of media in a tweet.
/// </summary>
public enum MediaType
{
    /// <summary>
    ///     A photo media type.
    /// </summary>
    Photo,

    /// <summary>
    ///     A video media type.
    /// </summary>
    Video,

    /// <summary>
    ///     An animated GIF media type.
    /// </summary>
    AnimatedGif
}

/// <summary>
///     Represents a video variant with its URL, bitrate, and dimensions.
/// </summary>
/// <param name="Url">The URL of the video variant.</param>
/// <param name="Bitrate">The bitrate of the video in bits per second.</param>
/// <param name="Width">The width of the video in pixels.</param>
/// <param name="Height">The height of the video in pixels.</param>
public record VideoVariant(string Url, int? Bitrate, int? Width, int? Height);

/// <summary>
///     Represents a tweet with its ID, text content, and associated media.
/// </summary>
/// <param name="Id">The unique identifier of the tweet.</param>
/// <param name="Text">The text content of the tweet.</param>
/// <param name="Media">An array of media objects associated with the tweet.</param>
public record Tweet(string Id, string Text, Media[] Media);

/// <summary>
///     Represents media content in a tweet.
/// </summary>
/// <param name="Type">The type of media (Photo, Video, or AnimatedGif).</param>
public abstract record Media(MediaType Type);

/// <summary>
///     Represents photo media content within a tweet.
/// </summary>
/// <param name="Url">The URL of the media content, if applicable.</param>
public record PhotoMedia(string Url) : Media(MediaType.Photo);

/// <summary>
///     Represents video or animated GIF media content, including available streaming variants.
/// </summary>
/// <param name="Type">The type of rich media (Video or AnimatedGif).</param>
/// <param name="Variants">An array of video variants, if the media is a video or animated GIF.</param>
public record VideoMedia(MediaType Type, VideoVariant[] Variants) : Media(Type);