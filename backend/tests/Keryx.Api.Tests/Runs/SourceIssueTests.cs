using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using Keryx.Application.Collection;
using Keryx.Domain.Collection;
using Keryx.Domain.Sources;
using Keryx.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Keryx.Api.Tests.Runs;

// Acceptance Test
// Traces to: L2-020 AC1, AC4, AC5; L2-007 AC4
// Description: A source that fails is an issue on the run; the same failure on consecutive runs
// extends one issue's streak from its first time; a success closes it, and a later failure is a
// new issue. Held items are a warning issue. Each test has its own store and clock.
public sealed class SourceIssueTests
{
    // A query string, which no issue detail may keep (L2-020 AC5).
    private const string Gone = "https://gone.example/feed?utm_source=keryx";
    private const string Global = "https://worldfaithfit.org/feed";
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    [Fact]
    public async Task Given_a_source_fails_the_same_way_on_4_consecutive_runs_When_the_last_run_is_read_Then_its_issue_has_a_streak_of_4_since_the_first()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, Gone, "Gone Gym", "toronto");
        var first = factory.Time.GetUtcNow();
        var runs = new List<string>();
        for (var i = 0; i < 4; i++)
        {
            runs.Add(await RunAsync(factory));
            factory.Time.Advance(Interval);
        }

        using var client = await StaffClient.OpenAsync(factory);
        var issue = Assert.Single((await ReadRunAsync(client, runs[3])).GetProperty("issues").EnumerateArray());
        Assert.Equal("error", issue.GetProperty("severity").GetString());
        Assert.Equal("source_not_found", issue.GetProperty("kind").GetString());
        Assert.Equal("source", issue.GetProperty("subject_type").GetString());
        Assert.StartsWith("src_", issue.GetProperty("subject_id").GetString(), StringComparison.Ordinal);
        Assert.Equal("Gone Gym", issue.GetProperty("subject_name").GetString());
        Assert.Equal("Nothing was found at https://gone.example/feed.", issue.GetProperty("detail").GetString());
        Assert.Equal(4, issue.GetProperty("consecutive_runs").GetInt32());
        Assert.Equal(first, issue.GetProperty("since").GetDateTimeOffset());
        // The same issue is listed on the first of those runs.
        var earliest = Assert.Single((await ReadRunAsync(client, runs[0])).GetProperty("issues").EnumerateArray());
        Assert.Equal(issue.GetProperty("id").GetString(), earliest.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Given_a_failing_source_When_it_next_answers_Then_its_issue_is_closed_and_a_later_failure_is_a_new_issue()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, Gone, "Gone Gym", "toronto");
        var failed = await RunAsync(factory);
        factory.Time.Advance(Interval);
        factory.Sources.AnswerFeed(Gone, "<rss version=\"2.0\"><channel><title>Feed</title></channel></rss>");
        var answered = await RunAsync(factory);
        Assert.Equal([IssueState.Closed], await StatesAsync(factory));
        factory.Time.Advance(Interval);
        factory.Sources.Answer(Gone, Keryx.Application.Sources.SourceFetchResult.Failed(Keryx.Application.Sources.FetchFailureReason.NotFound));

        var again = await RunAsync(factory);

        using var client = await StaffClient.OpenAsync(factory);
        Assert.Empty((await ReadRunAsync(client, answered)).GetProperty("issues").EnumerateArray());
        var before = Assert.Single((await ReadRunAsync(client, failed)).GetProperty("issues").EnumerateArray());
        var after = Assert.Single((await ReadRunAsync(client, again)).GetProperty("issues").EnumerateArray());
        Assert.NotEqual(before.GetProperty("id").GetString(), after.GetProperty("id").GetString());
        Assert.Equal(1, after.GetProperty("consecutive_runs").GetInt32());
        Assert.Equal([IssueState.Closed, IssueState.Open], await StatesAsync(factory));
    }

    private static async Task<List<IssueState>> StatesAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<KeryxDbContext>().RunIssues.AsNoTracking()
            .OrderBy(issue => issue.Id).Select(issue => issue.State).ToListAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Given_the_run_between_two_failures_was_cut_short_When_the_source_fails_again_Then_the_streak_starts_over()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, Gone, "Gone Gym", "toronto");
        await RunAsync(factory);
        factory.Time.Advance(Interval);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            // A run that never reached the source, such as one interrupted by a stop.
            var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
            var cut = Run.Start(RunWorker.Collection, RunTrigger.Schedule, factory.Time.GetUtcNow());
            cut.Fail("interrupted", null, factory.Time.GetUtcNow());
            db.Runs.Add(cut);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        factory.Time.Advance(Interval);
        var last = await RunAsync(factory);

        using var client = await StaffClient.OpenAsync(factory);
        var issue = Assert.Single((await ReadRunAsync(client, last)).GetProperty("issues").EnumerateArray());
        Assert.Equal(1, issue.GetProperty("consecutive_runs").GetInt32());
        Assert.Equal(factory.Time.GetUtcNow(), issue.GetProperty("since").GetDateTimeOffset());
    }

    [Fact]
    public async Task Given_a_source_with_items_held_for_review_When_the_run_finishes_Then_it_carries_an_items_held_warning()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        // "running" and "faith" in the global region: 0.48, held.
        factory.Sources.AnswerFeed(Global,
            "<rss version=\"2.0\"><channel><title>Feed</title><item><title>Running and faith retreat</title><link>https://worldfaithfit.org/1</link></item></channel></rss>");
        await AddSourceAsync(factory, SourceKind.Rss, Global, "World Faith Fit", "global");

        var run = await RunAsync(factory);

        using var client = await StaffClient.OpenAsync(factory);
        var issue = Assert.Single((await ReadRunAsync(client, run)).GetProperty("issues").EnumerateArray());
        Assert.Equal("warning", issue.GetProperty("severity").GetString());
        Assert.Equal("items_held", issue.GetProperty("kind").GetString());
        Assert.Equal("World Faith Fit", issue.GetProperty("subject_name").GetString());
        Assert.Equal("1 item held for review: its relevance score is between 0.45 and 0.60.", issue.GetProperty("detail").GetString());
    }

    private static async Task<JsonElement> ReadRunAsync(HttpClient client, string runId) =>
        await client.GetFromJsonAsync<JsonElement>(new Uri($"/admin/v1/runs/{runId}", UriKind.Relative), TestContext.Current.CancellationToken);

    private static async Task AddSourceAsync(ApiFactory factory, SourceKind kind, string address, string name, string region)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
        db.Sources.Add(Source.Add(kind, address, name, region, factory.Time.GetUtcNow()));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A pending run named by the lease, as a slot makes it, then the pipeline over it.</summary>
    private static async Task<string> RunAsync(ApiFactory factory)
    {
        string runId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
            var run = Run.Start(RunWorker.Collection, RunTrigger.Schedule, factory.Time.GetUtcNow());
            db.Runs.Add(run);
            var lease = await db.RunLeases.SingleOrDefaultAsync(TestContext.Current.CancellationToken);
            if (lease is null)
            {
                lease = RunLease.For(RunWorker.Collection, factory.Time.GetUtcNow());
                db.RunLeases.Add(lease);
            }

            lease.Claim(run.PublicId);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            runId = run.PublicId;
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunCollectionCommand(), TestContext.Current.CancellationToken);
        }

        return runId;
    }
}
