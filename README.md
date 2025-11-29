# stdray.Twitter

A C# library for retrieving Twitter/X.com tweet content by ID.

## Description

`stdray.Twitter` provides a simple API for retrieving tweet data, including text and media files (images and videos). The library uses an authentication mechanism similar to yt-dlp to work with Twitter/X GraphQL API.

## Features

- Retrieve tweet data by ID
- Extract tweet text
- Extract media files (images, videos, animated GIFs)
- For videos: get all variants with bitrate and resolution information
- Automatic resolution extraction from URL if not specified in JSON

## Usage

```csharp
using stdray.Twitter;

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

## Building

This project uses Cake build automation system with GitVersion for semantic versioning. You can build the project in several ways:

### Using Cake (Cross-platform)

1. **Prerequisites**: Install .NET SDK 8.0 or later

2. **Run the build**:
   ```bash
   # On Windows
   .\build.ps1

   # On macOS/Linux
   chmod +x build.sh
   ./build.sh
   ```

### Using .NET CLI directly

```bash
# Restore packages
dotnet restore

# Build the solution
dotnet build -c Release

# Run tests
dotnet test -c Release

# Pack the NuGet package
dotnet pack -c Release
```

### Build Targets

The Cake build script supports the following targets:

- `Default`: Cleans, restores, builds and runs tests
- `Build`: Builds the solution
- `Test`: Runs unit tests
- `Pack`: Creates NuGet packages
- `Publish`: Publishes NuGet packages to nuget.org
- `CI`: Full CI pipeline (build, test, pack, publish)

Example: `.\build.ps1 -Target="Pack" -Configuration="Release"`

## CI/CD

This project uses GitHub Actions for continuous integration and deployment:
- Pull requests trigger build and test validation
- Commits to the `main` branch trigger the full CI/CD pipeline including NuGet package publishing

## License

See LICENSE file (if applicable).

