using System.Net;
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
using static Keryx.Api.Tests.Runs.CollectionRuns;

namespace Keryx.Api.Tests.Runs;

// Acceptance Test
// Traces to: L2-006 AC1, AC2, AC4; L2-047 AC1 (the numbers its notification reads)
// Description: Staff start a collection run at any time. The API records a manual run the worker
// then executes as it would a scheduled one; a second start while one runs is refused with the
// running run's id, and a manual run leaves the schedule as it was.
public sealed class StartRunTests
{
    private static readonly Uri Runs = new("/admin/v1/runs", UriKind.Relative);
    private static readonly Uri Schedule = new("/admin/v1/overview/schedule", UriKind.Relative);
    private static readonly string[] StartedKeys = ["estimated_duration_ms", "id", "sources_total"];

    [Fact]
    public async Task Given_no_run_in_progress_When_staff_start_one_Then_202_with_a_manual_run_the_sources_to_check_and_how_long_the_last_took()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, "https://redeemedrun.ca/feed", "Redeemed Run Club", "toronto");
        await AddSourceAsync(factory, SourceKind.Website, "https://templefit.ca", "Temple Fit", "toronto");
        await AddSourceAsync(factory, SourceKind.Rss, "https://covenantcycling.ca/feed", "Covenant Cycling", "toronto");
        // An Instagram account is read by other means: a run does not check it.
        await AddSourceAsync(factory, SourceKind.Instagram, "templefit", "Temple Fit", "toronto");
        // A retention run since then took longer: only collection runs say how long one takes.
        var retention = Run.Start(RunWorker.Retention, RunTrigger.Schedule, factory.Time.GetUtcNow().AddHours(-1));
        retention.Finish(new RunCounts(), factory.Time.GetUtcNow().AddMinutes(-40));
        await SaveRunsAsync(factory, Finished(factory.Time.GetUtcNow().AddHours(-12), 231_000), Finished(factory.Time.GetUtcNow().AddHours(-6), 252_000), retention);

        using var response = await client.PostAsync(Runs, content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var started = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(StartedKeys, started.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        var id = started.GetProperty("id").GetString()!;
        Assert.StartsWith("run_", id, StringComparison.Ordinal);
        Assert.Equal(3, started.GetProperty("sources_total").GetInt32());
        Assert.Equal(252_000, started.GetProperty("estimated_duration_ms").GetInt64());
        var run = await ReadRunAsync(client, id);
        Assert.Equal(("manual", "running"), (run.GetProperty("trigger").GetString(), run.GetProperty("outcome").GetString()));
        Assert.Equal(factory.Time.GetUtcNow(), run.GetProperty("started_at").GetDateTimeOffset());
    }

    [Fact]
    public async Task Given_no_finished_run_When_staff_start_one_Then_no_duration_is_estimated()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        await SaveRunsAsync(factory, Failed(factory.Time.GetUtcNow().AddHours(-6)));

        using var response = await client.PostAsync(Runs, content: null, TestContext.Current.CancellationToken);

        var started = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(JsonValueKind.Null, started.GetProperty("estimated_duration_ms").ValueKind);
        Assert.Equal(0, started.GetProperty("sources_total").GetInt32());
    }

    [Fact]
    public async Task Given_a_manual_run_When_the_worker_picks_it_up_Then_it_runs_and_finishes_as_a_scheduled_one_would()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, "https://redeemedrun.ca/feed", "Redeemed Run Club", "toronto");
        factory.Sources.AnswerFeed("https://redeemedrun.ca/feed", Feed());
        var id = await StartAsync(client);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunCollectionCommand(), TestContext.Current.CancellationToken);
        }

        var run = await ReadRunAsync(client, id);
        Assert.Equal(("manual", "succeeded"), (run.GetProperty("trigger").GetString(), run.GetProperty("outcome").GetString()));
        Assert.Equal((1, 1), (run.GetProperty("sources_checked").GetInt32(), run.GetProperty("sources_total").GetInt32()));
    }

    [Fact]
    public async Task Given_a_run_in_progress_When_staff_start_another_Then_409_run_in_progress_with_its_id_and_no_run_is_made()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        var running = await StartAsync(client);

        using var response = await client.PostAsync(Runs, content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("run_in_progress", problem.GetProperty("code").GetString());
        Assert.Equal(running, problem.GetProperty("run_id").GetString());
        Assert.Equal(1, await CountRunsAsync(factory));
    }

    [Fact]
    public async Task Given_a_manual_run_When_it_has_started_and_finished_Then_the_next_scheduled_time_is_unchanged()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        // A worker that has handled today's slots: the lease names the latest.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new TickCollectionSchedule(), TestContext.Current.CancellationToken);
        }

        var before = await NextRunAtAsync(client);
        var slotBefore = await LastSlotAsync(factory);
        await StartAsync(client);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunCollectionCommand(), TestContext.Current.CancellationToken);
        }

        Assert.Equal(before, await NextRunAtAsync(client));
        Assert.Equal(slotBefore, await LastSlotAsync(factory));
    }

    [Fact]
    public async Task Given_no_worker_has_looked_at_the_schedule_When_staff_start_a_run_Then_the_slot_already_past_is_not_run_again()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        await StartAsync(client);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new RunCollectionCommand(), TestContext.Current.CancellationToken);
            await sender.Send(new TickCollectionSchedule(), TestContext.Current.CancellationToken);
        }

        // The first look at the schedule finds the latest slot handled, so it starts no run of its own.
        Assert.Equal(1, await CountRunsAsync(factory));
    }

    private static async Task<HttpClient> OpenAsync(ApiFactory factory)
    {
        await StaffClient.SeedAsync(factory);
        return await StaffClient.OpenAsync(factory);
    }

    internal static async Task<string> StartAsync(HttpClient client)
    {
        using var response = await client.PostAsync(Runs, content: null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var started = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return started.GetProperty("id").GetString()!;
    }

    private static Run Finished(DateTimeOffset at, long durationMs)
    {
        var run = Run.Start(RunWorker.Collection, RunTrigger.Schedule, at);
        run.Finish(new RunCounts { Added = 3, SourcesOk = 3 }, at.AddMilliseconds(durationMs));
        return run;
    }

    private static Run Failed(DateTimeOffset at)
    {
        var run = Run.Start(RunWorker.Collection, RunTrigger.Schedule, at);
        run.Fail("database_unreachable", correlationId: null, at.AddSeconds(48));
        return run;
    }

    private static async Task SaveRunsAsync(ApiFactory factory, params Run[] runs)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
        db.Runs.AddRange(runs);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<int> CountRunsAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<KeryxDbContext>().Runs.CountAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<DateTimeOffset> LastSlotAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
        return (await db.RunLeases.SingleAsync(TestContext.Current.CancellationToken)).LastScheduledSlotAt;
    }

    private static async Task<DateTimeOffset> NextRunAtAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>(Schedule, TestContext.Current.CancellationToken)).GetProperty("next_run_at").GetDateTimeOffset();
}
