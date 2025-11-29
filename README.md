# Karbonio.Twittertter

A C# library for retrieving Twitter/X.com tweet content by ID.

## Description

`Karbonio.Twitter` provides a simple API for retrieving tweet data, including text and media files (images and videos). The library uses an authentication mechanism similar to yt-dlp to work with Twitter/X GraphQL API.

## Features

- Retrieve tweet data by ID
- Extract tweet text
- Extract media files (images, videos, animated GIFs)
- For videos: get all variants with bitrate and resolution information
- Automatic resolution extraction from URL if not specified in JSON

## Usage

```csharp
using Karbonio.Twitter;

var client = new TwitterClient(new HttpClient());
var tweet = await client.GetTweetByIdAsync("1989071142053900550");

Console.WriteLine($"Text: {tweet.Text}");
Console.WriteLine($"Media: {tweet.Media.Length}");

foreach (var media in tweet.Media)
{
    if (media.Type == MediaType.Photo)
    {
        Console.WriteLine($"Image: {media.Url}");
    }
    else if (media.Type == MediaType.Video && media.Variants != null)
    {
        // Sort variants by bitrate
        var bestVariant = media.Variants
            .OrderByDescending(v => v.Bitrate ?? 0)
            .First();
        Console.WriteLine($"Video: {bestVariant.Url} ({bestVariant.Width}x{bestVariant.Height})");
    }
}
```

## Source

This library is based on the authentication mechanism and Twitter API logic from the [yt-dlp](https://github.com/yt-dlp/yt-dlp) project.

Source code reference:
- [`yt_dlp/extractor/twitter.py`](https://github.com/yt-dlp/yt-dlp/blob/master/yt_dlp/extractor/twitter.py)

## Requirements

- .NET 8.0

## License

See LICENSE file (if applicable).

