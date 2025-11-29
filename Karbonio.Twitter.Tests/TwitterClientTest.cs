using Karbonio.Twitter;
using Xunit;

namespace Karbonio.Twitter.Tests;

public class TwitterClientTest
{
    [Fact]
    public async Task GetTweetByIdAsync_ShouldReturnCorrectMediaCount()
    {
        using var httpClient = new HttpClient();
        var client = new TwitterClient(httpClient);
        var tweetId = "1989071142053900550";

        var tweet = await client.GetTweetByIdAsync(tweetId);

        var imageCount = tweet.Media.Count(m => m.Type == MediaType.Photo);
        var videoCount = tweet.Media.Count(m => m.Type == MediaType.Video || m.Type == MediaType.AnimatedGif);

        Assert.Equal(3, imageCount);
        Assert.Equal(1, videoCount);
    }
}

