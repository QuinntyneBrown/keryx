using Keryx.Api.Tests.Fakes;
using Keryx.Application.Seeding;
using Keryx.Domain.Collection;
using Keryx.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Keryx.Api.Tests.Workers;

// Acceptance Test
// Traces to: L2-005, L2-019
// Description: The worker's scheduler looks at the schedule on the clock it is given, starts the
// run a slot is due for, and keeps going after a tick that fails. It never sleeps: each step
// waits until the scheduler waits on the clock, then advances it.
public sealed class CollectionSchedulerTests
{
    private const string Scheduler = "Keryx.Worker.CollectionScheduler";
    private static readonly TimeSpan Recheck = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Within = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Given_the_worker_is_running_at_10_00_When_the_clock_reaches_the_12_00_slot_Then_one_scheduled_run_starts()
    {
        await using var worker = new WorkerHarness(FakeSchemaVersion.AtExpected);
        await SeedAsync(worker);
        await worker.StartAsync();
        await worker.Time.TimerAsync(Recheck, Within);

        // 10:01 to 11:59: a tick a minute, and no run. Then 12:00 Toronto, 17:00 UTC.
        for (var minute = 1; minute < 120; minute++)
        {
            worker.Time.Advance(Recheck);
            await worker.Time.TimerAsync(Recheck, Within);
        }

        Assert.Empty(await RunsAsync(worker));
        worker.Time.Advance(Recheck);
        await worker.Time.TimerAsync(Recheck, Within);
        await worker.StopAsync();

        var run = Assert.Single(await RunsAsync(worker));
        Assert.Equal(new DateTimeOffset(2026, 11, 27, 17, 0, 0, TimeSpan.Zero), run.StartedAt);
        Assert.Equal(RunTrigger.Schedule, run.Trigger);
    }

    [Fact]
    public async Task Given_the_worker_starts_40_seconds_before_a_slot_When_it_has_ticked_Then_it_waits_40_seconds_not_a_minute()
    {
        await using var worker = new WorkerHarness(FakeSchemaVersion.AtExpected);
        await SeedAsync(worker);
        // 40 seconds: no other timer of the worker has that due time (the gate's is 30, the runner's 10).
        worker.Time.SetUtcNow(new DateTimeOffset(2026, 11, 27, 16, 59, 20, TimeSpan.Zero));
        await worker.StartAsync();

        await worker.Time.TimerAsync(TimeSpan.FromSeconds(40), Within);
        worker.Time.Advance(TimeSpan.FromSeconds(40));
        await worker.Time.TimerAsync(Recheck, Within);
        await worker.StopAsync();

        Assert.Single(await RunsAsync(worker));
    }

    [Fact]
    public async Task Given_a_tick_that_throws_When_the_scheduler_catches_it_Then_it_logs_no_message_and_looks_again_a_minute_later()
    {
        // No seed: there is no settings row, so the tick throws.
        await using var worker = new WorkerHarness(FakeSchemaVersion.AtExpected);
        await worker.StartAsync();
        var error = await worker.Logs.WaitForAsync(line => line.Category == Scheduler && line.Level == LogLevel.Error, TestContext.Current.CancellationToken);
        await worker.Time.TimerAsync(Recheck, Within);
        Assert.DoesNotContain("Sequence contains no elements", error.Message, StringComparison.Ordinal);
        Assert.Contains("InvalidOperationException", error.Message, StringComparison.Ordinal);

        await SeedAsync(worker);
        worker.Time.Advance(Recheck);
        await worker.Time.TimerAsync(Recheck, Within);
        await worker.StopAsync();

        await using var scope = worker.Services.CreateAsyncScope();
        Assert.Single(await scope.ServiceProvider.GetRequiredService<KeryxDbContext>().RunLeases.ToListAsync(TestContext.Current.CancellationToken));
    }

    private static async Task SeedAsync(WorkerHarness worker)
    {
        await using var scope = worker.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new SeedDataCommand(), TestContext.Current.CancellationToken);
    }

    private static async Task<List<Run>> RunsAsync(WorkerHarness worker)
    {
        await using var scope = worker.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<KeryxDbContext>().Runs.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
    }
}
