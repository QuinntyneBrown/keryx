using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using Keryx.Domain.Collection;
using Keryx.Domain.Sources;
using Keryx.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using static Keryx.Api.Tests.Runs.CollectionRuns;

namespace Keryx.Api.Tests.Runs;

// Acceptance Test
// Traces to: L2-043 AC1–AC4 (the data), L2-046 AC2 (has_sources, has_runs), L2-005 AC2
// Description: The top of Overview in one read: the last finished collection run, the one in
// progress, whether any source or run exists, and when the next run is due. Counts and durations
// are numbers; a failure is a code a client branches on.
public sealed class OverviewStatusTests
{
    private static readonly Uri Status = new("/admin/v1/overview/status", UriKind.Relative);
    private static readonly string[] StatusKeys = ["current_run", "has_runs", "has_sources", "last_run", "next_run_at", "time_zone"];
    private static readonly string[] RunKeys =
        ["added", "duration_ms", "failure_code", "finished_at", "id", "outcome", "sources_checked", "sources_ok", "sources_total", "started_at"];

    /// <summary>28 November 2026, 09:46 in Toronto: the next 6-hourly run is at 12:00 (17:00 UTC).</summary>
    private static readonly DateTimeOffset NineFortySix = new(2026, 11, 28, 14, 46, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_no_source_and_no_run_When_the_status_is_read_Then_both_runs_are_null_and_nothing_exists()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            // The seed adds a news query; this install has none.
            var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
            db.Sources.RemoveRange(db.Sources);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var status = await client.GetFromJsonAsync<JsonElement>(Status, TestContext.Current.CancellationToken);

        Assert.Equal(StatusKeys, status.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(JsonValueKind.Null, status.GetProperty("last_run").ValueKind);
        Assert.Equal(JsonValueKind.Null, status.GetProperty("current_run").ValueKind);
        Assert.False(status.GetProperty("has_sources").GetBoolean());
        Assert.False(status.GetProperty("has_runs").GetBoolean());
        Assert.Equal(new DateTimeOffset(2026, 11, 28, 17, 0, 0, TimeSpan.Zero), status.GetProperty("next_run_at").GetDateTimeOffset());
        Assert.Equal("America/Toronto", status.GetProperty("time_zone").GetString());
    }

    [Fact]
    public async Task Given_a_run_that_completed_with_2_failing_sources_When_the_status_is_read_Then_it_is_the_last_run_with_its_numbers()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, "https://redeemedrun.ca/feed", "Redeemed Run Club", "toronto");
        var started = factory.Time.GetUtcNow().AddHours(-3).AddMinutes(-46);
        var run = Started(started, 64, 64);
        run.Finish(new RunCounts { Added = 47, SourcesOk = 62, SourcesFailed = 2 }, started.AddSeconds(252));
        var older = Started(started.AddHours(-6), 64, 64);
        older.Finish(new RunCounts { Added = 19, SourcesOk = 64 }, started.AddHours(-6).AddMinutes(4));
        await SaveAsync(factory, older, run, Run.Start(RunWorker.Retention, RunTrigger.Schedule, started.AddMinutes(1)));

        var status = await client.GetFromJsonAsync<JsonElement>(Status, TestContext.Current.CancellationToken);

        var last = status.GetProperty("last_run");
        Assert.Equal(RunKeys, last.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(run.PublicId, last.GetProperty("id").GetString());
        Assert.Equal("completed_with_issues", last.GetProperty("outcome").GetString());
        Assert.Equal(started, last.GetProperty("started_at").GetDateTimeOffset());
        Assert.Equal(252_000, last.GetProperty("duration_ms").GetInt64());
        Assert.Equal((47, 62, 64, 64), (last.GetProperty("added").GetInt32(), last.GetProperty("sources_ok").GetInt32(),
            last.GetProperty("sources_checked").GetInt32(), last.GetProperty("sources_total").GetInt32()));
        Assert.Equal(JsonValueKind.Null, last.GetProperty("failure_code").ValueKind);
        Assert.Equal(JsonValueKind.Null, status.GetProperty("current_run").ValueKind);
        Assert.True(status.GetProperty("has_sources").GetBoolean());
        Assert.True(status.GetProperty("has_runs").GetBoolean());
    }

    [Fact]
    public async Task Given_a_run_in_progress_When_the_status_is_read_Then_it_is_the_current_run_with_the_sources_checked_so_far()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        var started = factory.Time.GetUtcNow().AddMinutes(-3);
        var last = Started(started.AddHours(-6), 64, 64);
        last.Finish(new RunCounts { Added = 47, SourcesOk = 64 }, started.AddHours(-6).AddMinutes(4));
        await SaveAsync(factory, last, Started(started, 64, 38));

        var status = await client.GetFromJsonAsync<JsonElement>(Status, TestContext.Current.CancellationToken);

        var current = status.GetProperty("current_run");
        Assert.Equal("running", current.GetProperty("outcome").GetString());
        Assert.Equal((38, 64), (current.GetProperty("sources_checked").GetInt32(), current.GetProperty("sources_total").GetInt32()));
        Assert.Equal(JsonValueKind.Null, current.GetProperty("finished_at").ValueKind);
        Assert.Equal(JsonValueKind.Null, current.GetProperty("duration_ms").ValueKind);
        Assert.Equal(last.PublicId, status.GetProperty("last_run").GetProperty("id").GetString());
    }

    [Fact]
    public async Task Given_only_the_first_run_in_progress_When_the_status_is_read_Then_a_run_exists_and_there_is_no_last_run()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        await SaveAsync(factory, Started(factory.Time.GetUtcNow().AddMinutes(-1), 64, 3));

        var status = await client.GetFromJsonAsync<JsonElement>(Status, TestContext.Current.CancellationToken);

        Assert.Equal(JsonValueKind.Null, status.GetProperty("last_run").ValueKind);
        Assert.Equal(JsonValueKind.Object, status.GetProperty("current_run").ValueKind);
        Assert.True(status.GetProperty("has_runs").GetBoolean());
    }

    [Theory]
    [InlineData("store", "store_failed")]
    [InlineData("interrupted", "interrupted")]
    [InlineData("unexpected", "unexpected")]
    public async Task Given_the_last_run_failed_When_the_status_is_read_Then_its_failure_is_a_code(string how, string code)
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        var started = factory.Time.GetUtcNow().AddHours(-1);
        var run = Started(started, 64, 64);
        switch (how)
        {
            case "store":
                run.FailStage(PipelineStage.Store, "The records of this run could not be saved.", started.AddSeconds(48));
                break;
            case "interrupted":
                run.Fail("interrupted", null, started.AddSeconds(48));
                break;
            default:
                run.Fail("The run stopped unexpectedly.", "trace-1", started.AddSeconds(48));
                break;
        }

        await SaveAsync(factory, run);

        var status = await client.GetFromJsonAsync<JsonElement>(Status, TestContext.Current.CancellationToken);

        var last = status.GetProperty("last_run");
        Assert.Equal(("failed", code), (last.GetProperty("outcome").GetString(), last.GetProperty("failure_code").GetString()));
        Assert.Equal(0, last.GetProperty("sources_ok").GetInt32());
    }

    [Fact]
    public async Task Given_no_session_When_the_status_is_read_Then_401()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Status, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static Run Started(DateTimeOffset at, int total, int checkedSoFar)
    {
        var run = Run.Start(RunWorker.Collection, RunTrigger.Schedule, at);
        run.BeginExecution(total);
        for (var i = 0; i < checkedSoFar; i++)
        {
            run.SourceChecked();
        }

        return run;
    }

    private static async Task<HttpClient> OpenAsync(ApiFactory factory)
    {
        factory.Time.SetUtcNow(NineFortySix);
        await StaffClient.SeedAsync(factory);
        return await StaffClient.OpenAsync(factory);
    }

    private static async Task SaveAsync(ApiFactory factory, params Run[] runs)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
        db.Runs.AddRange(runs);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
