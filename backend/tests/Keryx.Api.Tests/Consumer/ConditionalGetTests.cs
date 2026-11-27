using System.Net;
using System.Net.Http.Headers;
using Keryx.Api.Tests.Session;
using static Keryx.Api.Tests.Consumer.ConsumerApi;

namespace Keryx.Api.Tests.Consumer;

// Acceptance Test
// Traces to: L2-032 AC1-AC3
// Description: A successful read carries an ETag and a short private lifetime; a product that
// sends the current ETag back gets 304 with no body, and changed content a new ETag.
// (Staff hiding an article is the change AC3 names; it arrives with journey 12. Here a newly
// published article changes the list.)
public sealed class ConditionalGetTests
{
    private const string Feed = "https://redeemedrun.ca/feed";
    private static readonly DateTimeOffset Day = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly Uri Articles = new("/v1/articles", UriKind.Relative);

    [Fact]
    public async Task Given_a_list_and_an_article_When_each_is_read_Then_each_carries_a_strong_ETag_and_a_private_60_second_lifetime()
    {
        using var factory = new ApiFactory();
        using var client = await SeededAsync(factory);
        var id = await ArticleIdAsync(factory, "https://redeemedrun.ca/n/apple");

        using var list = await client.GetAsync(Articles, TestContext.Current.CancellationToken);
        using var one = await client.GetAsync(new Uri($"/v1/articles/{id}", UriKind.Relative), TestContext.Current.CancellationToken);

        foreach (var response in new[] { list, one })
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(Assert.IsType<EntityTagHeaderValue>(response.Headers.ETag).IsWeak);
            AssertPrivateFor60Seconds(response);
        }

        Assert.NotEqual(list.Headers.ETag, one.Headers.ETag);
    }

    [Theory]
    [InlineData("{0}")]
    [InlineData("W/{0}")]
    [InlineData("\"other\", {0}")]
    [InlineData("*")]
    public async Task Given_the_current_ETag_When_it_is_sent_in_If_None_Match_Then_304_with_no_body_and_the_same_ETag(string ifNoneMatch)
    {
        using var factory = new ApiFactory();
        using var client = await SeededAsync(factory);
        using var first = await client.GetAsync(Articles, TestContext.Current.CancellationToken);
        var etag = first.Headers.ETag!.Tag;

        using var request = new HttpRequestMessage(HttpMethod.Get, Articles);
        request.Headers.TryAddWithoutValidation("If-None-Match", string.Format(System.Globalization.CultureInfo.InvariantCulture, ifNoneMatch, etag));
        using var second = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
        Assert.Empty(await second.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(etag, second.Headers.ETag?.Tag);
        AssertPrivateFor60Seconds(second);
    }

    [Fact]
    public async Task Given_a_stale_ETag_When_the_list_has_changed_Then_200_with_a_new_ETag_and_the_new_content()
    {
        using var factory = new ApiFactory();
        using var client = await SeededAsync(factory);
        using var first = await client.GetAsync(Articles, TestContext.Current.CancellationToken);
        var stale = first.Headers.ETag!;

        factory.Time.Advance(TimeSpan.FromMinutes(10));
        await CollectAsync(factory, "https://lifepointfit.ca/feed", "LifePoint Fitness", "toronto",
            new Item("Running birch", "https://lifepointfit.ca/n/birch", Day.AddHours(1)));
        using var request = new HttpRequestMessage(HttpMethod.Get, Articles);
        request.Headers.IfNoneMatch.Add(stale);
        using var second = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.NotEqual(stale.Tag, second.Headers.ETag?.Tag);
        Assert.Contains("Running birch", await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/v1/articles?limit=0")]
    [InlineData("/v1/articles/art_aaaaaaaaaaaa")]
    public async Task Given_a_read_that_fails_When_it_is_answered_Then_it_carries_no_ETag_and_is_not_cached(string path)
    {
        using var factory = new ApiFactory();
        using var client = await SeededAsync(factory);

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.True((int)response.StatusCode >= 400, $"{path} was {(int)response.StatusCode}");
        Assert.Null(response.Headers.ETag);
        Assert.DoesNotContain("max-age=60", response.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);
    }

    // Cache-Control: private, max-age=60, and nothing else.
    private static void AssertPrivateFor60Seconds(HttpResponseMessage response)
    {
        var cache = Assert.IsType<CacheControlHeaderValue>(response.Headers.CacheControl);
        Assert.Equal(
            (true, TimeSpan.FromSeconds(60), false, false, false, 0),
            (cache.Private, cache.MaxAge, cache.NoCache, cache.NoStore, cache.Public, cache.Extensions.Count));
    }

    private static async Task<HttpClient> SeededAsync(ApiFactory factory)
    {
        await StaffClient.SeedAsync(factory);
        await CollectAsync(factory, Feed, "Redeemed Run Club", "toronto", new Item("Running apple", "https://redeemedrun.ca/n/apple", Day));
        return Client(factory, await KeyAsync(factory, "articles:read"));
    }
}
