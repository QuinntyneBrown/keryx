using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using Keryx.Domain.Collection;
using Keryx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Keryx.Api.Tests.Consumer.ConsumerApi;

namespace Keryx.Api.Tests.Consumer;

// Acceptance Test
// Traces to: L2-030 AC3, AC4
// Description: Any valid key reads how fresh collection is: when the latest collection run
// started, how it went, when the next is due and how many articles are published; nothing about
// a source, an issue or an error.
public sealed class StatusApiTests
{
    // The fixture's clock reads 2026-11-27 15:00 UTC, 10:00 in Toronto. The default interval is
    // 6 hours from midnight Toronto time, so the next slot is 12:00 Toronto, 17:00 UTC.
    private static readonly DateTimeOffset NextSlot = new(2026, 11, 27, 17, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Day = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_no_run_has_ever_been_recorded_When_status_is_read_Then_last_run_and_status_are_null_and_the_next_slot_is_given()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        using var client = Client(factory, await KeyAsync(factory, "events:read"));

        var status = await StatusAsync(client);

        Assert.Equal(["articles_total", "last_run_at", "next_run_at", "status"], status.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal((JsonValueKind.Null, JsonValueKind.Null), (status.GetProperty("last_run_at").ValueKind, status.GetProperty("status").ValueKind));
        Assert.Equal((NextSlot, 0), (status.GetProperty("next_run_at").GetDateTimeOffset(), status.GetProperty("articles_total").GetInt32()));
    }

    [Fact]
    public async Task Given_a_run_with_a_failing_source_When_status_is_read_Then_it_is_completed_with_issues_counting_published_articles_and_naming_nothing()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        // Nothing answers the broken feed, so the run completes with an issue.
        await Keryx.Api.Tests.Runs.CollectionRuns.AddSourceAsync(factory, Keryx.Domain.Sources.SourceKind.Rss, "https://brokenfeed.ca/feed", "Broken Feed Club", "toronto");
        await CollectAsync(factory, "https://redeemedrun.ca/feed", "Redeemed Run Club", "toronto",
            new Item("Running apple", "https://redeemedrun.ca/n/apple", Day),
            new Item("Running birch", "https://redeemedrun.ca/n/birch", Day),
            new Item("Community bake sale", "https://redeemedrun.ca/n/bake", Day));
        var (started, outcome) = await LatestCollectionRunAsync(factory);
        using var client = Client(factory, await KeyAsync(factory, "events:read"));

        using var response = await client.GetAsync(new Uri("/v1/status", UriKind.Relative), TestContext.Current.CancellationToken);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var status = JsonDocument.Parse(text).RootElement;

        Assert.Equal(RunOutcome.CompletedWithIssues, outcome);
        Assert.Equal(
            ("completed_with_issues", started, 2),
            (status.GetProperty("status").GetString(), status.GetProperty("last_run_at").GetDateTimeOffset(), status.GetProperty("articles_total").GetInt32()));
        foreach (var secret in new[] { "Broken Feed Club", "brokenfeed", "redeemedrun", "not_found", "failure", "source" })
        {
            Assert.DoesNotContain(secret, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Given_the_latest_collection_run_failed_When_status_is_read_Then_it_is_failed_at_that_runs_start_and_articles_are_still_served()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await CollectAsync(factory, "https://redeemedrun.ca/feed", "Redeemed Run Club", "toronto", new Item("Running apple", "https://redeemedrun.ca/n/apple", Day));
        factory.Time.Advance(TimeSpan.FromHours(1));
        var failedAt = factory.Time.GetUtcNow();
        await AddRunAsync(factory, RunWorker.Collection, run => run.Fail("The database was unreachable.", null, failedAt.AddMinutes(1)));
        using var client = Client(factory, await KeyAsync(factory, "events:read", "articles:read"));

        var status = await StatusAsync(client);
        using var articles = await client.GetAsync(new Uri("/v1/articles", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(("failed", failedAt), (status.GetProperty("status").GetString(), status.GetProperty("last_run_at").GetDateTimeOffset()));
        Assert.Equal(HttpStatusCode.OK, articles.StatusCode);
        Assert.Equal(1, (await articles.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("data").GetArrayLength());
        Assert.DoesNotContain("unreachable", status.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Given_a_collection_run_in_progress_and_a_later_run_of_another_worker_When_status_is_read_Then_it_is_the_collection_run_running()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        var startedAt = factory.Time.GetUtcNow();
        await AddRunAsync(factory, RunWorker.Collection, _ => { });
        factory.Time.Advance(TimeSpan.FromMinutes(5));
        await AddRunAsync(factory, RunWorker.Retention, run => run.Fail("Retention failed.", null, factory.Time.GetUtcNow()));
        using var client = Client(factory, await KeyAsync(factory, "events:read"));

        var status = await StatusAsync(client);

        Assert.Equal(("running", startedAt), (status.GetProperty("status").GetString(), status.GetProperty("last_run_at").GetDateTimeOffset()));
    }

    [Fact]
    public async Task Given_the_clock_passes_a_slot_When_status_is_read_Then_next_run_at_is_the_slot_after_now()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        using var client = Client(factory, await KeyAsync(factory, "events:read"));
        factory.Time.Advance(TimeSpan.FromHours(2));

        Assert.Equal(NextSlot.AddHours(6), (await StatusAsync(client)).GetProperty("next_run_at").GetDateTimeOffset());
    }

    private static async Task AddRunAsync(ApiFactory factory, RunWorker worker, Action<Run> finish)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
        var run = Run.Start(worker, RunTrigger.Manual, factory.Time.GetUtcNow());
        finish(run);
        db.Runs.Add(run);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<(DateTimeOffset Started, RunOutcome Outcome)> LatestCollectionRunAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var run = await scope.ServiceProvider.GetRequiredService<KeryxDbContext>().Runs
            .Where(candidate => candidate.Worker == RunWorker.Collection).OrderByDescending(candidate => candidate.StartedAt)
            .FirstAsync(TestContext.Current.CancellationToken);
        return (run.StartedAt, run.Outcome);
    }

    private static async Task<JsonElement> StatusAsync(HttpClient client)
    {
        using var response = await client.GetAsync(new Uri("/v1/status", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }
}
