using System.Diagnostics.Metrics;
using Keryx.Api.Tests.Fakes;
using Keryx.Api.Tests.Workers;
using Keryx.Application.Seeding;
using Keryx.Application.Sources;
using Keryx.Domain.Collection;
using Keryx.Domain.Sources;
using Keryx.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry.Metrics;

namespace Keryx.Api.Tests.Telemetry;

// Acceptance Test
// Traces to: L2-094 AC3
// Description: A collection run records its duration, the items it added, updated and left
// unchanged, the sources that failed, and each source's fetch latency, named by the source's id.
public sealed class RunMetricsTests
{
    private const string Fed = "https://lifepointfit.ca/feed";
    private const string Broken = "https://brokenfit.example/feed";
    private static readonly TimeSpan Pickup = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Within = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan FetchTakes = TimeSpan.FromMilliseconds(1_500);

    [Fact]
    public async Task Given_a_run_with_a_fed_and_a_failing_source_When_it_executes_Then_its_duration_items_failures_and_fetch_latency_are_measured()
    {
        var exported = new ExportedCollection<Metric>();
        var sources = new FakeSourceFetcher();
        sources.AnswerFeed(Fed,
            "<rss version=\"2.0\"><channel><title>Feed</title>" +
            "<item><title>Church fitness night</title><link>https://lifepointfit.ca/e/1</link><description>Toronto</description></item>" +
            "<item><title>Faith and fitness walk</title><link>https://lifepointfit.ca/e/2</link><description>Toronto</description></item>" +
            "</channel></rss>");
        sources.Answer(Broken, SourceFetchResult.Failed(FetchFailureReason.Timeout));
        await using var worker = new WorkerHarness(FakeSchemaVersion.AtExpected, services =>
        {
            services.RemoveAll<ISourceFetcher>();
            services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddInMemoryExporter(exported));
            services.AddSingleton<ISourceFetcher>(provider => new SlowFetcher(sources, (FakeTimeProvider)provider.GetRequiredService<TimeProvider>()));
        });
        using var measured = new HostMeasurements(
            worker.Services.GetRequiredService<IMeterFactory>(),
            "keryx.run.duration", "keryx.run.items", "keryx.run.sources_failed", "keryx.fetch.duration");
        var (fedId, brokenId) = await SeedAsync(worker);
        var runId = await StartRunAsync(worker);

        await worker.StartAsync();
        await worker.Time.TimerAsync(Pickup, Within);
        Assert.True(worker.Services.GetRequiredService<MeterProvider>().ForceFlush());
        await worker.StopAsync();

        // Exported (by name: other hosts' runs are exported too)...
        Assert.Equal(
            ["keryx.fetch.duration", "keryx.run.duration", "keryx.run.items", "keryx.run.sources_failed"],
            exported.Snapshot().Select(metric => metric.Name).Where(name => name.StartsWith("keryx.", StringComparison.Ordinal)).Distinct().Order(StringComparer.Ordinal));
        // ...and measured by this host as follows.

        var run = await RunAsync(worker, runId);
        Assert.Equal((2, 0, 0, 1), (run.Counts.Added, run.Counts.Updated, run.Counts.Unchanged, run.Counts.SourcesFailed));
        var duration = Assert.Single(measured.Of("keryx.run.duration"));
        Assert.Equal(("s", (run.FinishedAt!.Value - run.StartedAt).TotalSeconds), (duration.Unit, duration.Value));
        Assert.Equal(new Dictionary<string, string> { ["keryx.run.outcome"] = "completed_with_issues" }, duration.Tags);
        Assert.Equal(
            [("added", 2d), ("unchanged", 0d), ("updated", 0d)],
            measured.Of("keryx.run.items").GroupBy(item => item.Tags["result"]).Select(group => (group.Key, group.Sum(item => item.Value))).OrderBy(pair => pair.Key, StringComparer.Ordinal));
        Assert.Equal(1d, measured.Of("keryx.run.sources_failed").Sum(failed => failed.Value));
        var fetches = measured.Of("keryx.fetch.duration").ToDictionary(fetch => Assert.Single(fetch.Tags, tag => tag.Key == "keryx.source.id").Value);
        Assert.Equal(new[] { brokenId, fedId }.Order(StringComparer.Ordinal), fetches.Keys.Order(StringComparer.Ordinal));
        Assert.All(fetches.Values, fetch => Assert.Equal("s", fetch.Unit));
        Assert.Equal(FetchTakes.TotalSeconds, fetches[fedId].Value);
        // The two are fetched at once, so the failing one may see the fed one's time pass.
        Assert.InRange(fetches[brokenId].Value, 0, FetchTakes.TotalSeconds);
    }

    /// <summary>Answers as the fake does; the fed source answers after the fixture's clock has moved on by <see cref="FetchTakes"/>.</summary>
    private sealed class SlowFetcher(FakeSourceFetcher inner, FakeTimeProvider time) : ISourceFetcher
    {
        public Task<SourceFetchResult> FetchAsync(SourceFetchRequest request, CancellationToken cancellationToken)
        {
            if (request.Address.AbsoluteUri == Fed)
            {
                time.Advance(FetchTakes);
            }

            return inner.FetchAsync(request, cancellationToken);
        }
    }

    private static async Task<(string Fed, string Broken)> SeedAsync(WorkerHarness worker)
    {
        await using var scope = worker.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new SeedDataCommand(), TestContext.Current.CancellationToken);
        var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
        var fed = Source.Add(SourceKind.Rss, Fed, "LifePoint Fitness Ministry", "toronto", worker.Time.GetUtcNow());
        var broken = Source.Add(SourceKind.Rss, Broken, "Broken Fitness", "toronto", worker.Time.GetUtcNow());
        db.Sources.AddRange(fed, broken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (fed.PublicId, broken.PublicId);
    }

    private static async Task<string> StartRunAsync(WorkerHarness worker)
    {
        await using var scope = worker.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
        var run = Run.Start(RunWorker.Collection, RunTrigger.Schedule, worker.Time.GetUtcNow());
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
