using Keryx.Api.Tests.Fakes;
using Keryx.Application.Alerts;
using Keryx.Application.Seeding;
using Keryx.Domain.Collection;
using Keryx.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Keryx.Api.Tests.Workers;

// Acceptance Test
// Traces to: L2-078 AC1, AC4; L2-019
// Description: The worker's digest service sends the digest request when the clock reaches 08:00
// Toronto time, and each morning after; its run is recorded with worker alert_digest. It never
// sleeps: it waits until the service waits on the clock, then advances it.
public sealed class AlertDigestWorkerTests
{
    private static readonly TimeSpan Within = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Given_the_worker_is_running_at_10_00_When_the_clock_reaches_08_00_Then_a_digest_run_is_recorded_and_the_next_waits_a_day()
    {
        await using var worker = new WorkerHarness(FakeSchemaVersion.AtExpected, services =>
        {
            services.RemoveAll<IMailSender>();
            services.AddSingleton<IMailSender>(new FakeMailSender());
        });
        await using (var scope = worker.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new SeedDataCommand(), TestContext.Current.CancellationToken);
        }

        await worker.StartAsync();
        // 10:00 Toronto (15:00 UTC) to 08:00 the next day: 22 hours.
        await worker.Time.TimerAsync(TimeSpan.FromHours(22), Within);
        Assert.Empty(await DigestRunsAsync(worker));

        worker.Time.Advance(TimeSpan.FromHours(22));
        await worker.Time.TimerAsync(TimeSpan.FromHours(24), Within);
        await worker.StopAsync();

        var run = Assert.Single(await DigestRunsAsync(worker));
        Assert.Equal(new DateTimeOffset(2026, 11, 28, 13, 0, 0, TimeSpan.Zero), run.StartedAt);
        Assert.Equal((RunTrigger.Schedule, RunOutcome.Succeeded), (run.Trigger, run.Outcome));
    }

    private static async Task<List<Run>> DigestRunsAsync(WorkerHarness worker)
    {
        await using var scope = worker.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<KeryxDbContext>().Runs.AsNoTracking()
            .Where(run => run.Worker == RunWorker.AlertDigest).ToListAsync(TestContext.Current.CancellationToken);
    }
}
