using Keryx.Api.Tests.Fakes;
using Keryx.Application.Seeding;
using Keryx.Domain.Catalogue;
using Keryx.Domain.Collection;
using Keryx.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Keryx.Api.Tests.Workers;

// Acceptance Test
// Traces to: L2-079 AC4; L2-019 AC1, AC4
// Description: The worker's retention service runs retention when the clock reaches 03:00 Toronto
// time, and each night after; its run is recorded with worker retention, trigger schedule and the
// number deleted. It never sleeps: it waits until the service waits on the clock, then advances it.
public sealed class RetentionWorkerTests
{
    private static readonly TimeSpan Within = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Given_the_worker_is_running_at_10_00_When_the_clock_reaches_03_00_Then_a_retention_run_deletes_what_expired_and_the_next_waits_a_day()
    {
        await using var worker = new WorkerHarness(FakeSchemaVersion.AtExpected);
        await using (var scope = worker.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new SeedDataCommand(), TestContext.Current.CancellationToken);
            var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
            db.Articles.Add(Article.Collect(
                new ArticleFacts("https://example.org/old", "Old", "All welcome.", "events", "toronto", [], null, 0.70m, PublicationStatus.Published),
                null, 1, "Example feed", worker.Time.GetUtcNow().AddYears(-3)));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await worker.StartAsync();
        // 10:00 Toronto (15:00 UTC) to 03:00 the next day: 17 hours.
        await worker.Time.TimerAsync(TimeSpan.FromHours(17), Within);
        Assert.Empty(await RetentionRunsAsync(worker));

        worker.Time.Advance(TimeSpan.FromHours(17));
        await worker.Time.TimerAsync(TimeSpan.FromHours(24), Within);
        await worker.StopAsync();

        var run = Assert.Single(await RetentionRunsAsync(worker));
        Assert.Equal(new DateTimeOffset(2026, 11, 28, 8, 0, 0, TimeSpan.Zero), run.StartedAt);
        Assert.Equal((RunTrigger.Schedule, RunOutcome.Succeeded, 1), (run.Trigger, run.Outcome, run.Counts.Deleted));
    }

    private static async Task<List<Run>> RetentionRunsAsync(WorkerHarness worker)
    {
        await using var scope = worker.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<KeryxDbContext>().Runs.AsNoTracking()
            .Where(run => run.Worker == RunWorker.Retention).ToListAsync(TestContext.Current.CancellationToken);
    }
}
