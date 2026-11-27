using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using Keryx.Domain.Catalogue;
using Keryx.Domain.Collection;
using Keryx.Domain.Sources;
using Keryx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Keryx.Api.Tests.Runs.CollectionRuns;

namespace Keryx.Api.Tests.Catalogue;

// Acceptance Test
// Traces to: L2-012 AC4, AC5; L2-013 AC3, AC4
// Description: Each collected article keeps the decision its score gave under the threshold in
// force when it was collected: its status, why, and that threshold. A later threshold applies only
// to items collected afterwards, and a staff decision is never overwritten by a run.
public sealed class PublishDecisionsTests
{
    private const string Toronto = "https://redeemedrun.ca/feed";
    private const string Global = "https://worldfaithfit.org/feed";
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    // Default settings, T = 0.60. In Toronto (a focus area, geography 1.0): one topic scores
    // 0.6 × 1/3 + 0.4 = 0.60, no topic 0.3 × 1.0 = 0.30. In the global region (0.2): two topics
    // score 0.6 × 2/3 + 0.08 = 0.48.
    private static readonly (string Title, string Link, string Summary) OneTopic = ("Sunrise running group meets weekly", "https://redeemedrun.ca/n/1", "All welcome.");
    private static readonly (string Title, string Link, string Summary) NoTopic = ("Community bake sale this Saturday", "https://redeemedrun.ca/n/2", "All welcome.");
    private static readonly (string Title, string Link, string Summary) TwoTopicsAbroad = ("Running and faith retreat", "https://worldfaithfit.org/1", "All welcome.");

    [Fact]
    public async Task Given_published_held_and_hidden_articles_When_each_is_read_Then_it_carries_its_status_reason_threshold_and_score()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, Toronto, "Redeemed Run Club", "toronto");
        await AddSourceAsync(factory, SourceKind.Rss, Global, "World Faith Fit", "global");
        factory.Sources.AnswerFeed(Toronto, Feed(OneTopic, NoTopic));
        factory.Sources.AnswerFeed(Global, Feed(TwoTopicsAbroad));

        await RunAsync(factory);

        using var client = await StaffClient.OpenAsync(factory);
        var published = await ReadAsync(factory, client, OneTopic.Link);
        Assert.Equal(("published", JsonValueKind.Null, 0.60m, 0.60m, "system"), Decision(published));
        var held = await ReadAsync(factory, client, TwoTopicsAbroad.Link);
        Assert.Equal(("needs_review", JsonValueKind.String, 0.48m, 0.60m, "system"), Decision(held));
        Assert.Equal("below_publish_threshold", held.GetProperty("status_reason").GetString());
        var hidden = await ReadAsync(factory, client, NoTopic.Link);
        Assert.Equal(("hidden", JsonValueKind.String, 0.30m, 0.60m, "system"), Decision(hidden));
        Assert.Equal("below_review_floor", hidden.GetProperty("status_reason").GetString());
        // A number with at most two decimal places (L2-012 AC4).
        Assert.Equal("0.48", held.GetProperty("relevance").GetRawText());
    }

    [Fact]
    public async Task Given_the_threshold_is_raised_to_0_65_When_the_next_run_collects_Then_a_stored_article_keeps_its_decision_and_a_new_item_at_0_60_is_held()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, Toronto, "Redeemed Run Club", "toronto");
        factory.Sources.AnswerFeed(Toronto, Feed(OneTopic));
        await RunAsync(factory);
        await SetThresholdAsync(factory, 0.65m);
        factory.Time.Advance(Interval);
        var later = ("Evening running club for beginners", "https://redeemedrun.ca/n/3", "All welcome.");
        factory.Sources.AnswerFeed(Toronto, Feed(OneTopic, later));

        await RunAsync(factory);

        using var client = await StaffClient.OpenAsync(factory);
        Assert.Equal(("published", JsonValueKind.Null, 0.60m, 0.60m, "system"), Decision(await ReadAsync(factory, client, OneTopic.Link)));
        Assert.Equal(("needs_review", JsonValueKind.String, 0.60m, 0.65m, "system"), Decision(await ReadAsync(factory, client, later.Item2)));
    }

    [Fact]
    public async Task Given_staff_decided_an_article_When_its_source_changes_it_Then_its_facts_update_and_its_status_stays()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, Toronto, "Redeemed Run Club", "toronto");
        factory.Sources.AnswerFeed(Toronto, Feed(OneTopic));
        await RunAsync(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            // Staff hid it (the decision journey 12 makes).
            var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
            var article = await db.Articles.SingleAsync(TestContext.Current.CancellationToken);
            db.Entry(article).Property(value => value.Status).CurrentValue = PublicationStatus.Hidden;
            db.Entry(article).Property(value => value.DecidedBy).CurrentValue = DecisionMaker.Staff;
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        factory.Time.Advance(Interval);
        // Two topics in Toronto now: 0.80, which would publish it.
        factory.Sources.AnswerFeed(Toronto, Feed(OneTopic with { Title = "Sunrise running group adds strength training" }));

        await RunAsync(factory);

        using var client = await StaffClient.OpenAsync(factory);
        var read = await ReadAsync(factory, client, OneTopic.Link);
        Assert.Equal("Sunrise running group adds strength training", read.GetProperty("title").GetString());
        Assert.Equal(("hidden", "staff"), (read.GetProperty("status").GetString(), read.GetProperty("decided_by").GetString()));
    }

    private static (string?, JsonValueKind, decimal, decimal, string?) Decision(JsonElement article) => (
        article.GetProperty("status").GetString(),
        article.GetProperty("status_reason").ValueKind,
        article.GetProperty("relevance").GetDecimal(),
        article.GetProperty("threshold").GetDecimal(),
        article.GetProperty("decided_by").GetString());

    private static async Task SetThresholdAsync(ApiFactory factory, decimal threshold)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
        var settings = await db.Settings.SingleAsync(TestContext.Current.CancellationToken);
        db.Entry(settings).Property(value => value.PublishThreshold).CurrentValue = threshold;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<JsonElement> ReadAsync(ApiFactory factory, HttpClient client, string url)
    {
        string id;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            id = await scope.ServiceProvider.GetRequiredService<KeryxDbContext>().Articles
                .Where(article => article.Url == url).Select(article => article.PublicId).SingleAsync(TestContext.Current.CancellationToken);
        }

        return await client.GetFromJsonAsync<JsonElement>(new Uri($"/admin/v1/articles/{id}", UriKind.Relative), TestContext.Current.CancellationToken);
    }
}
