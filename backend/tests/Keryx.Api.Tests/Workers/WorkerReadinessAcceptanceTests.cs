// Acceptance Test
// Traces to: L2-095
// Description: The worker runs no worker until the database is ready, says why, asks again every 30 seconds and never migrates

using Keryx.Api.Tests.Fakes;
using Microsoft.Extensions.Logging;

namespace Keryx.Api.Tests.Workers;

public sealed class WorkerReadinessAcceptanceTests
{
    private const string Gate = "Keryx.Worker.WorkerReadinessGate";

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    // L2-095 AC5.
    [Fact]
    public async Task Given_a_database_behind_the_build_When_the_worker_starts_Then_it_runs_no_worker_and_logs_that_migrate_must_be_run()
    {
        await using var worker = new WorkerHarness(FakeSchemaVersion.Behind);

        await worker.StartAsync();
        var warning = await worker.Logs.WaitForAsync(
            line => line.Category == Gate && line.Level == LogLevel.Warning, TestContext.Current.CancellationToken);
        var readyWhileBehind = worker.Gate.Ready.IsCompleted;
        await worker.StopAsync();

        Assert.Contains("migrate", warning.Message, StringComparison.Ordinal);
        Assert.False(readyWhileBehind);
        Assert.True(worker.Gate.Ready.IsCanceled);
        Assert.Equal(0, worker.Probe.Runs);
        Assert.Equal(0, worker.Migrator.Calls);
    }

    [Fact]
    public async Task Given_a_database_at_the_expected_version_When_the_worker_starts_Then_its_workers_run_after_one_check_and_no_warning()
    {
        await using var worker = new WorkerHarness(FakeSchemaVersion.AtExpected);

        await worker.StartAsync();
        await worker.Probe.WhenRanAsync(worker.Schema);
        await worker.StopAsync();

        Assert.Equal(1, worker.Probe.Runs);
        Assert.Equal(1, worker.Schema.Checks);
        Assert.Equal([LogLevel.Information], worker.Logs.Of(Gate).Select(line => line.Level));
        Assert.Equal(0, worker.Migrator.Calls);
    }

    // Stopping the host waits for the gate, so a check that 29 seconds had wrongly started would be counted.
    [Fact]
    public async Task Given_a_database_that_is_not_ready_When_29_seconds_pass_Then_the_worker_has_not_asked_again()
    {
        await using var worker = new WorkerHarness(FakeSchemaVersion.Behind);
        await worker.StartAsync();
        await worker.Schema.WhenCheckedAsync(1, TestContext.Current.CancellationToken);

        worker.Time.Advance(TimeSpan.FromSeconds(29));
        await worker.StopAsync();

        Assert.Equal(1, worker.Schema.Checks);
        Assert.Equal(0, worker.Probe.Runs);
    }

    [Fact]
    public async Task Given_migrate_was_run_while_the_worker_waited_When_30_seconds_have_passed_Then_it_asks_again_and_its_workers_run()
    {
        await using var worker = new WorkerHarness(FakeSchemaVersion.Behind);
        await worker.StartAsync();
        await worker.Schema.WhenCheckedAsync(1, TestContext.Current.CancellationToken);

        worker.Schema.State = FakeSchemaVersion.AtExpected;
        worker.Time.Advance(Interval);
        await worker.Probe.WhenRanAsync(worker.Schema);
        await worker.StopAsync();

        Assert.Equal(2, worker.Schema.Checks);
        Assert.Equal(1, worker.Probe.Runs);
        Assert.Equal([LogLevel.Warning, LogLevel.Information], worker.Logs.Of(Gate).Select(line => line.Level));
        Assert.Equal(0, worker.Migrator.Calls);
    }

    // One warning when it starts waiting, not one per check.
    [Fact]
    public async Task Given_a_database_that_stays_behind_When_the_worker_asks_three_times_Then_it_warns_once()
    {
        await using var worker = new WorkerHarness(FakeSchemaVersion.Behind);
        await worker.StartAsync();
        await worker.Schema.WhenCheckedAsync(1, TestContext.Current.CancellationToken);

        worker.Time.Advance(Interval);
        await worker.Schema.WhenCheckedAsync(2, TestContext.Current.CancellationToken);
        worker.Time.Advance(Interval);
        await worker.Schema.WhenCheckedAsync(3, TestContext.Current.CancellationToken);
        await worker.StopAsync();

        Assert.Equal(3, worker.Schema.Checks);
        Assert.Single(worker.Logs.Of(Gate));
        Assert.Equal(0, worker.Probe.Runs);
    }

    // The reason is said again only when it changes: unreachable, then behind, then ready.
    [Fact]
    public async Task Given_a_database_that_is_unreachable_then_behind_then_ready_When_the_worker_asks_Then_it_says_each_reason_once()
    {
        await using var worker = new WorkerHarness(FakeSchemaVersion.Unreachable);
        await worker.StartAsync();
        await worker.Schema.WhenCheckedAsync(1, TestContext.Current.CancellationToken);

        worker.Schema.State = FakeSchemaVersion.Behind;
        worker.Time.Advance(Interval);
        await worker.Schema.WhenCheckedAsync(2, TestContext.Current.CancellationToken);
        worker.Schema.State = FakeSchemaVersion.AtExpected;
        worker.Time.Advance(Interval);
        await worker.Probe.WhenRanAsync(worker.Schema);
        await worker.StopAsync();

        var lines = worker.Logs.Of(Gate);
        Assert.Equal([LogLevel.Warning, LogLevel.Warning, LogLevel.Information], lines.Select(line => line.Level));
        Assert.Contains("cannot be reached", lines[0].Message, StringComparison.Ordinal);
        Assert.DoesNotContain("migrate", lines[0].Message, StringComparison.Ordinal);
        Assert.Contains("migrate", lines[1].Message, StringComparison.Ordinal);
        Assert.Equal(3, worker.Schema.Checks);
    }
}
