namespace stdray.Twitter.Tests;

public class TwitterClientTest
{
    [Theory]
    [MemberData(nameof(TweetData))]
    public async Task GetTweetByIdAsync_ShouldReturnCorrectMediaCountAndTextContent(string tweetId,
        int expectedImageCount, int expectedVideoCount, string expectedText)
    {
        using var httpClient = new HttpClient();
        var client = new TwitterClient(httpClient);

        var tweet = await client.GetTweetById(tweetId);

        var imageCount = tweet.Media.Count(m => m.Type == MediaType.Photo);
        var videoCount = tweet.Media.Count(m => m.Type is MediaType.Video or MediaType.AnimatedGif);

        Assert.Equal(expectedImageCount, imageCount);
        Assert.Equal(expectedVideoCount, videoCount);
        Assert.Contains(expectedText, tweet.Text, StringComparison.OrdinalIgnoreCase);
    }
    
    public static TheoryData<string, int, int, string> TweetData => new()
    {
        { "1989071142053900550", 3, 1, "доФфига" }
    };
}