using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using Keryx.Domain.Sources;
using Keryx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Keryx.Api.Tests.Runs.CollectionRuns;

namespace Keryx.Api.Tests.Catalogue;

// Acceptance Test
// Traces to: L2-010 AC1, AC2, AC3; L2-009 AC1
// Description: One story found through two sources is one article listing both: the same
// canonical link always merges, and with merging on a headline of the same organization within
// 14 days that is at least 0.90 alike merges too. A merged item collected again is unchanged.
public sealed class MergeAcrossSourcesTests
{
    private const string Feed1 = "https://redeemedrun.ca/feed";
    private const string Feed2 = "https://news.redeemedrun.ca/rss";
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    private static readonly (string Title, string Link, string Summary) Story =
        ("Toronto church running club starts spring 5K training for refugees", "https://redeemedrun.ca/news/spring-5k", "Weekly runs from the church.");

    [Fact]
    public async Task Given_an_article_from_one_source_When_another_source_returns_the_same_link_Then_no_second_article_is_made_and_both_sources_are_listed()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        var first = await AddSourceAsync(factory, SourceKind.Rss, Feed1, "Redeemed Run Club", "toronto");
        factory.Sources.AnswerFeed(Feed1, Feed(Story));
        await RunAsync(factory);
        factory.Time.Advance(Interval);
        var second = await AddSourceAsync(factory, SourceKind.Rss, Feed2, "Toronto Faith News", "toronto");
        factory.Sources.AnswerFeed(Feed2, Feed(Story with { Link = Story.Link + "?utm_source=news", Title = "Running club news" }));

        var runId = await RunAsync(factory);

        using var client = await StaffClient.OpenAsync(factory);
        var run = await ReadRunAsync(client, runId);
        Assert.Equal((0, 1, 1), (run.GetProperty("added").GetInt32(), run.GetProperty("duplicates").GetInt32(), run.GetProperty("unchanged").GetInt32()));
        var article = Assert.Single(await ReadArticlesAsync(factory, client));
        Assert.Equal(Story.Title, article.GetProperty("title").GetString());
        Assert.Equal([first, second], article.GetProperty("provenance").EnumerateArray().Select(entry => entry.GetProperty("source_id").GetString()));
    }

    [Fact]
    public async Task Given_two_sources_return_the_same_link_in_one_run_When_it_finishes_Then_one_article_lists_both()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, Feed1, "Redeemed Run Club", "toronto");
        await AddSourceAsync(factory, SourceKind.Rss, Feed2, "Toronto Faith News", "toronto");
        factory.Sources.AnswerFeed(Feed1, Feed(Story));
        factory.Sources.AnswerFeed(Feed2, Feed(Story));

        var runId = await RunAsync(factory);

        using var client = await StaffClient.OpenAsync(factory);
        var run = await ReadRunAsync(client, runId);
        Assert.Equal((1, 1), (run.GetProperty("added").GetInt32(), run.GetProperty("duplicates").GetInt32()));
        var article = Assert.Single(await ReadArticlesAsync(factory, client));
        Assert.Equal(2, article.GetProperty("provenance").GetArrayLength());
    }

    [Fact]
    public async Task Given_merging_on_When_the_same_organization_publishes_a_headline_one_word_in_ten_apart_Then_it_merges_and_is_unchanged_next_run()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, Feed1, "Redeemed Run Club", "toronto");
        await AddSourceAsync(factory, SourceKind.Rss, Feed2, "The Redeemed Run Club", "toronto");
        factory.Sources.AnswerFeed(Feed1, Feed(Story));
        // Ten words, one changed: 0.90.
        factory.Sources.AnswerFeed(Feed2, Feed(("Toronto church running club starts spring 10K training for refugees", "https://news.redeemedrun.ca/p/88", "Weekly runs.")));
        var first = await RunAsync(factory);
        factory.Time.Advance(Interval);

        var second = await RunAsync(factory);

        using var client = await StaffClient.OpenAsync(factory);
        var one = await ReadRunAsync(client, first);
        Assert.Equal((1, 1), (one.GetProperty("added").GetInt32(), one.GetProperty("duplicates").GetInt32()));
        var two = await ReadRunAsync(client, second);
        Assert.Equal((0, 0, 0, 2), (two.GetProperty("added").GetInt32(), two.GetProperty("updated").GetInt32(), two.GetProperty("duplicates").GetInt32(), two.GetProperty("unchanged").GetInt32()));
        // Sources are read in the order they answer, so either item may be the one the other merges into.
        var article = Assert.Single(await ReadArticlesAsync(factory, client));
        string[] links = ["https://news.redeemedrun.ca/p/88", "https://redeemedrun.ca/news/spring-5k"];
        Assert.Contains(article.GetProperty("url").GetString(), links);
        Assert.Equal(links, article.GetProperty("provenance").EnumerateArray().Select(entry => entry.GetProperty("url").GetString()).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Given_merging_on_When_two_words_in_ten_differ_or_the_organization_differs_Then_each_is_its_own_article()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, Feed1, "Redeemed Run Club", "toronto");
        await AddSourceAsync(factory, SourceKind.Rss, Feed2, "Redeemed Run Club", "toronto");
        await AddSourceAsync(factory, SourceKind.Rss, "https://lifepointfit.ca/feed", "LifePoint Fitness", "toronto");
        factory.Sources.AnswerFeed(Feed1, Feed(Story));
        // 0.80: two words changed.
        factory.Sources.AnswerFeed(Feed2, Feed(("Toronto church running club starts autumn 10K training for refugees", "https://news.redeemedrun.ca/p/88", "Weekly runs.")));
        // The same headline from another organization.
        factory.Sources.AnswerFeed("https://lifepointfit.ca/feed", Feed(Story with { Link = "https://lifepointfit.ca/e/5k" }));

        var runId = await RunAsync(factory);

        using var client = await StaffClient.OpenAsync(factory);
        var run = await ReadRunAsync(client, runId);
        Assert.Equal((3, 0), (run.GetProperty("added").GetInt32(), run.GetProperty("duplicates").GetInt32()));
    }

    [Fact]
    public async Task Given_an_article_of_one_organization_When_another_organization_later_publishes_the_same_headline_Then_it_is_its_own_article()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, Feed1, "Redeemed Run Club", "toronto");
        factory.Sources.AnswerFeed(Feed1, Feed(Story));
        await RunAsync(factory);
        factory.Time.Advance(Interval);
        await AddSourceAsync(factory, SourceKind.Rss, "https://lifepointfit.ca/feed", "LifePoint Fitness", "toronto");
        factory.Sources.AnswerFeed("https://lifepointfit.ca/feed", Feed(Story with { Link = "https://lifepointfit.ca/e/5k" }));

        var runId = await RunAsync(factory);

        using var client = await StaffClient.OpenAsync(factory);
        var run = await ReadRunAsync(client, runId);
        Assert.Equal((1, 0), (run.GetProperty("added").GetInt32(), run.GetProperty("duplicates").GetInt32()));
    }

    [Fact]
    public async Task Given_merging_off_When_two_sources_return_the_same_story_under_different_links_Then_two_articles_are_stored_and_a_same_link_still_matches()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
            var settings = await db.Settings.SingleAsync(TestContext.Current.CancellationToken);
            db.Entry(settings).Property(value => value.MergeDuplicates).CurrentValue = false;
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await AddSourceAsync(factory, SourceKind.Rss, Feed1, "Redeemed Run Club", "toronto");
        await AddSourceAsync(factory, SourceKind.Rss, Feed2, "Redeemed Run Club", "toronto");
        factory.Sources.AnswerFeed(Feed1, Feed(Story));
        factory.Sources.AnswerFeed(Feed2, Feed(Story with { Link = "https://news.redeemedrun.ca/p/88" }, Story with { Link = Story.Link + "#comments" }));

        var runId = await RunAsync(factory);

        using var client = await StaffClient.OpenAsync(factory);
        var run = await ReadRunAsync(client, runId);
        Assert.Equal((2, 1), (run.GetProperty("added").GetInt32(), run.GetProperty("duplicates").GetInt32()));
    }

    [Fact]
    public async Task Given_an_alike_headline_collected_more_than_14_days_after_the_article_When_it_is_collected_Then_it_does_not_merge()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, Feed1, "Redeemed Run Club", "toronto");
        factory.Sources.AnswerFeed(Feed1, Feed(Story));
        await RunAsync(factory);
        // 14 days later is still inside the window; a moment past it is not.
        factory.Time.Advance(TimeSpan.FromDays(14));
        await AddSourceAsync(factory, SourceKind.Rss, Feed2, "Redeemed Run Club", "toronto");
        factory.Sources.AnswerFeed(Feed2, Feed(Story with { Link = "https://news.redeemedrun.ca/p/1" }));
        var inside = await RunAsync(factory);
        factory.Time.Advance(TimeSpan.FromSeconds(1));
        factory.Sources.AnswerFeed(Feed2, Feed(Story with { Link = "https://news.redeemedrun.ca/p/1" }, Story with { Link = "https://news.redeemedrun.ca/p/2" }));

        var outside = await RunAsync(factory);

        using var client = await StaffClient.OpenAsync(factory);
        Assert.Equal(1, (await ReadRunAsync(client, inside)).GetProperty("duplicates").GetInt32());
        var late = await ReadRunAsync(client, outside);
        Assert.Equal((1, 0), (late.GetProperty("added").GetInt32(), late.GetProperty("duplicates").GetInt32()));
    }

    private static async Task<List<JsonElement>> ReadArticlesAsync(ApiFactory factory, HttpClient client)
    {
        List<string> ids;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            ids = await scope.ServiceProvider.GetRequiredService<KeryxDbContext>().Articles.OrderBy(article => article.Id)
                .Select(article => article.PublicId).ToListAsync(TestContext.Current.CancellationToken);
        }

        var articles = new List<JsonElement>();
        foreach (var id in ids)
        {
            articles.Add(await client.GetFromJsonAsync<JsonElement>(new Uri($"/admin/v1/articles/{id}", UriKind.Relative), TestContext.Current.CancellationToken));
        }

        return articles;
    }
}
