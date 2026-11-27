using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using Keryx.Application.Collection;
using Keryx.Domain.Collection;
using Keryx.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Keryx.Api.Tests.Runs;

// Acceptance Test
// Traces to: L2-005
// Description: The schedule starts one run at each slot in Toronto time, and a slot that arrives
// while a run is in progress is recorded as a skipped start on that run instead of a second run.
// Each test has its own store and clock; the seed's interval is 6 hours.
public sealed class ScheduleCollectionTests
{
    /// <summary>28 November 2026, 05:59:59 in Toronto (EST, UTC−5).</summary>
    private static readonly DateTimeOffset BeforeSix = new(2026, 11, 28, 10, 59, 59, TimeSpan.Zero);

    [Fact]
    public async Task Given_6_hours_and_the_clock_at_05_59_59_When_it_advances_one_second_Then_exactly_one_scheduled_run_starts()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        factory.Time.SetUtcNow(BeforeSix);
        // A worker started between slots waits for the next one: midnight's slot is past.
        await TickAsync(factory);
        await TickAsync(factory);
        Assert.Empty(await RunsAsync(factory));

        factory.Time.Advance(TimeSpan.FromSeconds(1));
        await TickAsync(factory);
        await TickAsync(factory);

        var run = Assert.Single(await RunsAsync(factory));
        Assert.Equal(RunTrigger.Schedule, run.Trigger);
        Assert.Equal(RunWorker.Collection, run.Worker);
        Assert.Equal(BeforeSix.AddSeconds(1), run.StartedAt);
        using var client = await StaffClient.OpenAsync(factory);
        var body = await client.GetFromJsonAsync<JsonElement>(new Uri($"/admin/v1/runs/{run.PublicId}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal("running", body.GetProperty("outcome").GetString());
        Assert.Equal("schedule", body.GetProperty("trigger").GetString());
    }

    [Fact]
    public async Task Given_a_run_in_progress_When_the_next_slot_arrives_Then_no_second_run_starts_and_the_skip_is_an_issue_on_the_running_run()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        factory.Time.SetUtcNow(BeforeSix);
        await TickAsync(factory);
        factory.Time.Advance(TimeSpan.FromSeconds(1));
        await TickAsync(factory);

        factory.Time.Advance(TimeSpan.FromHours(6));
        await TickAsync(factory);

        var running = Assert.Single(await RunsAsync(factory));
        using var client = await StaffClient.OpenAsync(factory);
        var body = await client.GetFromJsonAsync<JsonElement>(new Uri($"/admin/v1/runs/{running.PublicId}", UriKind.Relative), TestContext.Current.CancellationToken);
        var issue = Assert.Single(body.GetProperty("issues").EnumerateArray());
        Assert.Equal(
            ["id", "severity", "kind", "subject_type", "subject_id", "subject_name", "detail", "consecutive_runs", "since", "muted"],
            issue.EnumerateObject().Select(property => property.Name));
        Assert.StartsWith("iss_", issue.GetProperty("id").GetString(), StringComparison.Ordinal);
        Assert.Equal("warning", issue.GetProperty("severity").GetString());
        Assert.Equal("run_skipped", issue.GetProperty("kind").GetString());
        Assert.Equal("worker", issue.GetProperty("subject_type").GetString());
        Assert.Equal("collection", issue.GetProperty("subject_id").GetString());
        Assert.Equal("A scheduled start was skipped because this run was still in progress.", issue.GetProperty("detail").GetString());
        Assert.Equal(1, issue.GetProperty("consecutive_runs").GetInt32());
        Assert.Equal(BeforeSix.AddSeconds(1).AddHours(6), issue.GetProperty("since").GetDateTimeOffset());
        Assert.False(issue.GetProperty("muted").GetBoolean());

        // The run ends; the next run starts at the following slot, not at once.
        await FinishAsync(factory, running.PublicId);
        await TickAsync(factory);
        Assert.Single(await RunsAsync(factory));
        factory.Time.Advance(TimeSpan.FromHours(6));
        await TickAsync(factory);
        Assert.Equal(2, (await RunsAsync(factory)).Count);
        await using var scope = factory.Services.CreateAsyncScope();
        var skip = await scope.ServiceProvider.GetRequiredService<KeryxDbContext>().RunIssues.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(IssueState.Closed, skip.State);
    }

    [Fact]
    public async Task Given_a_tick_When_it_returns_Then_the_next_wake_is_the_next_slot_or_one_minute_whichever_is_sooner()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        // 09:46 in Toronto: the next slot is 12:00, so the tick looks again in a minute.
        factory.Time.SetUtcNow(new DateTimeOffset(2026, 11, 28, 14, 46, 0, TimeSpan.Zero));
        Assert.Equal(new DateTimeOffset(2026, 11, 28, 14, 47, 0, TimeSpan.Zero), (await TickAsync(factory)).NextWakeAt);

        // 11:59:30: the slot comes first.
        factory.Time.SetUtcNow(new DateTimeOffset(2026, 11, 28, 16, 59, 30, TimeSpan.Zero));
        Assert.Equal(new DateTimeOffset(2026, 11, 28, 17, 0, 0, TimeSpan.Zero), (await TickAsync(factory)).NextWakeAt);
    }

    private static async Task<ScheduleTickResult> TickAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new TickCollectionSchedule(), TestContext.Current.CancellationToken);
    }

    private static async Task<List<Run>> RunsAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<KeryxDbContext>().Runs.AsNoTracking()
            .OrderBy(run => run.StartedAt).ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>What the pipeline does at the end of a run: finish it and release the lease.</summary>
    private static async Task FinishAsync(ApiFactory factory, string runId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
        var run = await db.Runs.SingleAsync(candidate => candidate.PublicId == runId, TestContext.Current.CancellationToken);
        run.Finish(new RunCounts(), factory.Time.GetUtcNow());
        (await db.RunLeases.SingleAsync(TestContext.Current.CancellationToken)).Release(runId);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
