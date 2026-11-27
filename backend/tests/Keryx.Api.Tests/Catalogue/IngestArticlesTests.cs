using System.Net;
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
// Traces to: L2-009 AC1, AC2, AC3, AC4; L2-011 AC1, AC3; L2-015 AC1
// Description: A run stores each item a source published as one article with its provenance;
// a second run over the same items changes nothing, a changed title is an update that keeps the
// article's id and first-seen time, and a link with tracking parameters is the same article.
public sealed class IngestArticlesTests
{
    private const string Address = "https://redeemedrun.ca/feed";
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    private static readonly (string Title, string Link, string Summary)[] Five =
    [
        ("Toronto church running club starts spring 5K training", "https://redeemedrun.ca/news/spring-5k", "Weekly runs from the church."),
        ("Sunrise prayer and fitness bootcamp in Toronto", "https://redeemedrun.ca/news/bootcamp", "A bootcamp before work."),
        ("Faith and fitness night for Toronto youth", "https://redeemedrun.ca/news/youth", "Games, prayer and a workout."),
        ("Toronto Christian gym opens a second studio", "https://redeemedrun.ca/news/studio", "A new studio in the east end."),
        ("Healthy meals workshop at a Toronto church", "https://redeemedrun.ca/news/meals", "Cooking for families."),
    ];

    [Fact]
    public async Task Given_a_source_returns_the_same_5_items_on_two_runs_When_the_second_finishes_Then_it_adds_and_updates_nothing_and_5_articles_exist()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, Address, "Redeemed Run Club", "toronto");
        factory.Sources.AnswerFeed(Address, Feed(Five));
        var first = await RunAsync(factory);
        factory.Time.Advance(Interval);

        var second = await RunAsync(factory);

        using var client = await StaffClient.OpenAsync(factory);
        var one = await ReadRunAsync(client, first);
        Assert.Equal((5, 0, 0), (one.GetProperty("added").GetInt32(), one.GetProperty("updated").GetInt32(), one.GetProperty("unchanged").GetInt32()));
        var two = await ReadRunAsync(client, second);
        Assert.Equal((0, 0, 5), (two.GetProperty("added").GetInt32(), two.GetProperty("updated").GetInt32(), two.GetProperty("unchanged").GetInt32()));
        var store = two.GetProperty("stages").EnumerateArray().Single(stage => stage.GetProperty("stage").GetString() == "store");
        Assert.Equal(5, store.GetProperty("counts").GetProperty("unchanged").GetInt32());
        Assert.Equal(5, await ArticleCountAsync(factory));
    }

    [Fact]
    public async Task Given_an_article_When_it_is_read_Then_it_carries_its_fields_and_one_provenance_entry()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        var sourceId = await AddSourceAsync(factory, SourceKind.Rss, Address, "Redeemed Run Club", "toronto");
        factory.Sources.AnswerFeed(Address, Feed(Five[0]));
        var collected = factory.Time.GetUtcNow();
        await RunAsync(factory);

        using var client = await StaffClient.OpenAsync(factory);
        var article = await ReadArticleAsync(client, await ArticleIdAsync(factory, Five[0].Link));

        Assert.StartsWith("art_", article.GetProperty("id").GetString(), StringComparison.Ordinal);
        Assert.Equal(Five[0].Title, article.GetProperty("title").GetString());
        Assert.Equal(Five[0].Summary, article.GetProperty("summary").GetString());
        Assert.Equal(Five[0].Link, article.GetProperty("url").GetString());
        Assert.Equal("races-challenges", article.GetProperty("category").GetString());
        Assert.Equal("toronto", article.GetProperty("region").GetString());
        Assert.Equal(collected, article.GetProperty("collected_at").GetDateTimeOffset());
        Assert.Equal(sourceId, article.GetProperty("source").GetProperty("id").GetString());
        var provenance = Assert.Single(article.GetProperty("provenance").EnumerateArray());
        Assert.Equal(sourceId, provenance.GetProperty("source_id").GetString());
        Assert.Equal("Redeemed Run Club", provenance.GetProperty("source_name").GetString());
        Assert.Equal(Five[0].Link, provenance.GetProperty("url").GetString());
        Assert.Equal(collected, provenance.GetProperty("first_seen_at").GetDateTimeOffset());
        Assert.Equal(collected, provenance.GetProperty("last_seen_at").GetDateTimeOffset());
    }

    [Fact]
    public async Task Given_an_article_collected_again_unchanged_When_it_is_read_Then_first_seen_stays_and_last_seen_is_the_later_run_start()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, Address, "Redeemed Run Club", "toronto");
        factory.Sources.AnswerFeed(Address, Feed(Five[0]));
        var firstRun = factory.Time.GetUtcNow();
        await RunAsync(factory);
        var id = await ArticleIdAsync(factory, Five[0].Link);
        factory.Time.Advance(Interval);
        var secondRun = factory.Time.GetUtcNow();

        await RunAsync(factory);

        using var client = await StaffClient.OpenAsync(factory);
        var article = await ReadArticleAsync(client, id);
        Assert.Equal(id, article.GetProperty("id").GetString());
        var provenance = Assert.Single(article.GetProperty("provenance").EnumerateArray());
        Assert.Equal(firstRun, provenance.GetProperty("first_seen_at").GetDateTimeOffset());
        Assert.Equal(secondRun, provenance.GetProperty("last_seen_at").GetDateTimeOffset());
    }

    [Fact]
    public async Task Given_an_item_whose_title_changes_between_runs_When_the_second_finishes_Then_it_counts_one_update_and_the_article_keeps_its_id()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, Address, "Redeemed Run Club", "toronto");
        factory.Sources.AnswerFeed(Address, Feed(Five[0], Five[1]));
        var firstRun = factory.Time.GetUtcNow();
        await RunAsync(factory);
        var id = await ArticleIdAsync(factory, Five[0].Link);
        factory.Time.Advance(Interval);
        factory.Sources.AnswerFeed(Address, Feed(Five[0] with { Title = "Toronto church running club starts spring 10K training" }, Five[1]));

        var second = await RunAsync(factory);

        using var client = await StaffClient.OpenAsync(factory);
        var run = await ReadRunAsync(client, second);
        Assert.Equal((0, 1, 1), (run.GetProperty("added").GetInt32(), run.GetProperty("updated").GetInt32(), run.GetProperty("unchanged").GetInt32()));
        var article = await ReadArticleAsync(client, id);
        Assert.Equal("Toronto church running club starts spring 10K training", article.GetProperty("title").GetString());
        var provenance = Assert.Single(article.GetProperty("provenance").EnumerateArray());
        Assert.Equal(firstRun, provenance.GetProperty("first_seen_at").GetDateTimeOffset());
        Assert.Equal(factory.Time.GetUtcNow(), provenance.GetProperty("last_seen_at").GetDateTimeOffset());
        Assert.Equal(2, await ArticleCountAsync(factory));
    }

    [Fact]
    public async Task Given_an_item_returned_with_tracking_parameters_and_a_fragment_When_it_is_collected_again_Then_it_matches_the_article()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, Address, "Redeemed Run Club", "toronto");
        factory.Sources.AnswerFeed(Address, Feed(Five[0]));
        await RunAsync(factory);
        factory.Time.Advance(Interval);
        factory.Sources.AnswerFeed(Address, Feed(Five[0] with { Link = Five[0].Link + "?utm_source=rss&fbclid=x&gclid=y#top" }));

        var second = await RunAsync(factory);

        using var client = await StaffClient.OpenAsync(factory);
        var run = await ReadRunAsync(client, second);
        Assert.Equal((0, 0, 1), (run.GetProperty("added").GetInt32(), run.GetProperty("updated").GetInt32(), run.GetProperty("unchanged").GetInt32()));
        Assert.Equal(1, await ArticleCountAsync(factory));
    }

    [Theory]
    [InlineData("art_doesnotexist")]
    [InlineData("not-an-id")]
    public async Task Given_an_unknown_article_id_When_it_is_read_Then_404_article_not_found(string id)
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);

        using var response = await client.GetAsync(new Uri($"/admin/v1/articles/{id}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("article_not_found", problem.GetProperty("code").GetString());
    }

    private static async Task<JsonElement> ReadArticleAsync(HttpClient client, string id) =>
        await client.GetFromJsonAsync<JsonElement>(new Uri($"/admin/v1/articles/{id}", UriKind.Relative), TestContext.Current.CancellationToken);

    private static async Task<int> ArticleCountAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<KeryxDbContext>().Articles.CountAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<string> ArticleIdAsync(ApiFactory factory, string url)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<KeryxDbContext>().Articles
            .Where(article => article.Url == url).Select(article => article.PublicId).SingleAsync(TestContext.Current.CancellationToken);
    }
}
