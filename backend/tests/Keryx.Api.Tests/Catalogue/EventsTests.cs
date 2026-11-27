using System.Security;
using Keryx.Api.Tests.Session;
using Keryx.Domain.Sources;
using Keryx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Keryx.Api.Tests.Runs.CollectionRuns;

namespace Keryx.Api.Tests.Catalogue;

// Acceptance Test
// Traces to: L2-016 AC; L2-010 AC4; L2-011 AC1; L2-018
// Description: An item that publishes a start time and a place (the RSS event module) is also an
// event: in UTC with its zone, linked to its article, belonging to the source's organization. The
// same event found through two sources is one event listing both.
public sealed class EventsTests
{
    private const string Feed1 = "https://redeemedrun.ca/feed";
    private const string Feed2 = "https://news.redeemedrun.ca/rss";
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    [Fact]
    public async Task Given_an_item_with_a_local_start_and_a_place_When_it_is_collected_twice_Then_one_event_exists_in_UTC_with_its_zone_linked_to_its_article()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, Feed1, "Redeemed Run Club", "toronto");
        // A local time with no zone, and an end before the start, which is dropped.
        factory.Sources.AnswerFeed(Feed1, EventFeed(("Sunrise 5K run at Trinity Bellwoods for refugees", "https://redeemedrun.ca/e/sunrise", "2026-12-05T08:00", "2026-12-05T07:00", "Trinity Bellwoods Park")));
        var seen = factory.Time.GetUtcNow();
        await RunAsync(factory);
        factory.Time.Advance(Interval);

        var second = await RunAsync(factory);

        using (var client = await StaffClient.OpenAsync(factory))
        {
            var run = await ReadRunAsync(client, second);
            Assert.Equal((0, 1), (run.GetProperty("updated").GetInt32(), run.GetProperty("unchanged").GetInt32()));
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
        var item = Assert.Single(await db.Events.AsNoTracking().Include(item => item.Organization).Include(item => item.LinkedArticle)
            .ToListAsync(TestContext.Current.CancellationToken));
        Assert.StartsWith("evt_", item.PublicId, StringComparison.Ordinal);
        Assert.Equal("Sunrise 5K run at Trinity Bellwoods for refugees", item.Title);
        Assert.Equal(new DateTimeOffset(2026, 12, 5, 13, 0, 0, TimeSpan.Zero), item.StartsAtUtc);
        Assert.Equal("America/Toronto", item.TimeZone);
        Assert.Null(item.EndsAtUtc);
        Assert.Equal("Trinity Bellwoods Park", item.PlaceName);
        Assert.Equal("toronto", item.RegionSlug);
        Assert.Equal("Redeemed Run Club", item.Organization!.Name);
        Assert.Equal("https://redeemedrun.ca/e/sunrise", item.LinkedArticle!.Url);
        var provenance = Assert.Single(item.Provenance);
        Assert.Equal((seen, seen + Interval), (provenance.FirstSeenAt, provenance.LastSeenAt));
    }

    [Fact]
    public async Task Given_a_source_moves_its_event_by_three_hours_When_it_is_collected_again_Then_the_same_event_has_the_new_start_and_the_run_counts_an_update()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, Feed1, "Redeemed Run Club", "toronto");
        factory.Sources.AnswerFeed(Feed1, EventFeed(("Sunrise 5K run at Trinity Bellwoods for refugees", "https://redeemedrun.ca/e/sunrise", "2026-12-05T08:00", "", "Trinity Bellwoods Park")));
        await RunAsync(factory);
        string id;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            id = await scope.ServiceProvider.GetRequiredService<KeryxDbContext>().Events.Select(item => item.PublicId).SingleAsync(TestContext.Current.CancellationToken);
        }

        factory.Time.Advance(Interval);
        factory.Sources.AnswerFeed(Feed1, EventFeed(("Sunrise 5K run at Trinity Bellwoods for refugees", "https://redeemedrun.ca/e/sunrise", "2026-12-05T11:00", "", "Trinity Bellwoods Park")));

        var runId = await RunAsync(factory);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var item = Assert.Single(await scope.ServiceProvider.GetRequiredService<KeryxDbContext>().Events.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken));
            Assert.Equal(id, item.PublicId);
            Assert.Equal(new DateTimeOffset(2026, 12, 5, 16, 0, 0, TimeSpan.Zero), item.StartsAtUtc);
        }

        using var client = await StaffClient.OpenAsync(factory);
        Assert.Equal(1, (await ReadRunAsync(client, runId)).GetProperty("updated").GetInt32());
    }

    [Fact]
    public async Task Given_the_same_event_through_two_sources_When_both_are_collected_Then_one_event_lists_both()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, Feed1, "Redeemed Run Club", "toronto");
        await AddSourceAsync(factory, SourceKind.Rss, Feed2, "The Redeemed Run Club", "toronto");
        factory.Sources.AnswerFeed(Feed1, EventFeed(("Sunrise 5K run at Trinity Bellwoods for refugees", "https://redeemedrun.ca/e/sunrise", "2026-12-05T08:00:00-05:00", "", "Trinity Bellwoods Park")));
        // Different words and link, 45 minutes later, the same place spelled another way.
        factory.Sources.AnswerFeed(Feed2, EventFeed(("Join our December community run", "https://news.redeemedrun.ca/p/12", "2026-12-05T13:45:00Z", "2026-12-05T15:00:00Z", "trinity bellwoods park.")));

        await RunAsync(factory);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
        var item = Assert.Single(await db.Events.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, item.Provenance.Count);
        Assert.Equal(2, await db.Articles.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_events_of_one_organization_more_than_an_hour_apart_or_at_another_place_When_collected_Then_each_is_its_own_event()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, Feed1, "Redeemed Run Club", "toronto");
        factory.Sources.AnswerFeed(Feed1, EventFeed(
            ("Sunrise 5K run at Trinity Bellwoods for refugees", "https://redeemedrun.ca/e/1", "2026-12-05T08:00", "", "Trinity Bellwoods Park"),
            ("Second wave of the sunrise run", "https://redeemedrun.ca/e/2", "2026-12-05T09:01", "", "Trinity Bellwoods Park"),
            ("Sunrise run in the west end", "https://redeemedrun.ca/e/3", "2026-12-05T08:00", "", "High Park"),
            ("A notice with a time and no place", "https://redeemedrun.ca/e/4", "2026-12-05T08:00", "", "")));

        await RunAsync(factory);

        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(3, await scope.ServiceProvider.GetRequiredService<KeryxDbContext>().Events.CountAsync(TestContext.Current.CancellationToken));
    }

    private static string EventFeed(params (string Title, string Link, string Start, string End, string Place)[] items) =>
        "<rss version=\"2.0\" xmlns:ev=\"http://purl.org/rss/1.0/modules/event/\"><channel><title>Feed</title>" +
        string.Concat(items.Select(item =>
            $"<item><title>{SecurityElement.Escape(item.Title)}</title><link>{item.Link}</link><description>Bring a friend.</description>" +
            $"<ev:startdate>{item.Start}</ev:startdate>" +
            (item.End.Length > 0 ? $"<ev:enddate>{item.End}</ev:enddate>" : string.Empty) +
            (item.Place.Length > 0 ? $"<ev:location>{SecurityElement.Escape(item.Place)}</ev:location>" : string.Empty) +
            "</item>")) +
        "</channel></rss>";
}
