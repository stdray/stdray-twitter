using stdray.Twitter;
using Xunit;

namespace stdray.Twitter.Tests;

public class TwitterClientTest
{
    [Theory]
    [InlineData("1989071142053900550")]
    public async Task GetTweetByIdAsync_ShouldReturnCorrectMediaCountAndTextContent(string tweetId)
    {
        using var httpClient = new HttpClient();
        var client = new TwitterClient(httpClient);

        var tweet = await client.GetTweetById(tweetId);

        var imageCount = tweet.Media.Count(m => m.Type == MediaType.Photo);
        var videoCount = tweet.Media.Count(m => m.Type is MediaType.Video or MediaType.AnimatedGif);

        Assert.Equal(3, imageCount);
        Assert.Equal(1, videoCount);
        Assert.Contains("доФфига", tweet.Text, StringComparison.OrdinalIgnoreCase);
    }
}

