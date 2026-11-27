using Keryx.Api.Tests.Fakes;
using Keryx.Application.Seeding;
using Keryx.Application.Sources;
using Keryx.Domain.Collection;
using Keryx.Domain.Sources;
using Keryx.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Keryx.Api.Tests.Workers;

// Acceptance Test
// Traces to: L2-007, L2-019 AC5
// Description: Once the database is ready, the worker records any run a stop cut short as
// interrupted, then picks up the pending collection run and looks again every 10 seconds.
public sealed class CollectionRunnerTests
{
    private static readonly TimeSpan Pickup = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Within = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Given_a_pending_run_When_the_worker_starts_Then_it_runs_it_and_looks_again_10_seconds_later()
    {
        var sources = new FakeSourceFetcher();
        sources.AnswerFeed("https://lifepointfit.ca/feed", "<rss version=\"2.0\"><channel><title>Feed</title></channel></rss>");
        await using var worker = new WorkerHarness(FakeSchemaVersion.AtExpected, services =>
        {
            services.RemoveAll<ISourceFetcher>();
            services.AddSingleton<ISourceFetcher>(sources);
        });
        await SeedAsync(worker);
        var runId = await StartRunAsync(worker, executing: false);

        await worker.StartAsync();
        await worker.Time.TimerAsync(Pickup, Within);
        await worker.StopAsync();

        Assert.Equal(RunOutcome.Succeeded, (await RunAsync(worker, runId)).Outcome);
        Assert.Single(sources.Requests);
    }

    [Fact]
    public async Task Given_a_run_was_executing_when_the_worker_stopped_When_it_starts_again_Then_that_run_is_failed_interrupted_before_any_new_run()
    {
        await using var worker = new WorkerHarness(FakeSchemaVersion.AtExpected);
        await SeedAsync(worker);
        var runId = await StartRunAsync(worker, executing: true);

        await worker.StartAsync();
        await worker.Time.TimerAsync(Pickup, Within);
        await worker.StopAsync();

        var run = await RunAsync(worker, runId);
        Assert.Equal(RunOutcome.Failed, run.Outcome);
        Assert.Equal("interrupted", run.FailureReason);
    }

    private static async Task SeedAsync(WorkerHarness worker)
    {
        await using var scope = worker.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new SeedDataCommand(), TestContext.Current.CancellationToken);
        var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
        db.Sources.Add(Source.Add(SourceKind.Rss, "https://lifepointfit.ca/feed", "LifePoint Fitness Ministry", "toronto", worker.Time.GetUtcNow()));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<string> StartRunAsync(WorkerHarness worker, bool executing)
    {
        await using var scope = worker.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
        var run = Run.Start(RunWorker.Collection, RunTrigger.Schedule, worker.Time.GetUtcNow());
        if (executing)
        {
            run.BeginExecution(1);
        }

        var lease = RunLease.For(RunWorker.Collection, worker.Time.GetUtcNow());
        lease.Claim(run.PublicId);
        db.Runs.Add(run);
        db.RunLeases.Add(lease);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return run.PublicId;
    }

    private static async Task<Run> RunAsync(WorkerHarness worker, string runId)
    {
        await using var scope = worker.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<KeryxDbContext>().Runs.AsNoTracking()
            .SingleAsync(run => run.PublicId == runId, TestContext.Current.CancellationToken);
    }
}
