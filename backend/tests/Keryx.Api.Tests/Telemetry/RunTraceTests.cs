using System.Diagnostics;
using Keryx.Api.Tests.Fakes;
using Keryx.Api.Tests.Session;
using Keryx.Api.Tests.Workers;
using Keryx.Application.Abstractions;
using Keryx.Application.Alerts;
using Keryx.Application.Catalogue;
using Keryx.Application.Seeding;
using Keryx.Application.Sources;
using Keryx.Domain.Catalogue;
using Keryx.Domain.Collection;
using Keryx.Domain.Sources;
using Keryx.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Trace;

namespace Keryx.Api.Tests.Telemetry;

// Acceptance Test
// Traces to: L2-094 AC3; L2-019 AC3
// Description: One trace covers a collection run, with a span per stage and per source fetch.
// A fetch span names its source by id, never by its address or the address's query, and a
// pickup with no run to execute traces nothing.
public sealed class RunTraceTests
{
    private const string Secret = "s3cr3t-feed-token";
    private const string Fed = "https://lifepointfit.ca/feed?token=" + Secret;
    private const string Broken = "https://brokenfit.example/feed?token=" + Secret;
    private static readonly TimeSpan Pickup = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Within = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Given_a_pending_run_When_the_worker_executes_it_Then_one_trace_covers_it_with_a_span_per_stage_and_per_fetch()
    {
        var spans = new ExportedCollection<Activity>();
        var sources = new FakeSourceFetcher();
        sources.AnswerFeed(Fed, "<rss version=\"2.0\"><channel><title>Feed</title></channel></rss>");
        sources.Answer(Broken, SourceFetchResult.Failed(FetchFailureReason.Timeout));
        await using var worker = new WorkerHarness(FakeSchemaVersion.AtExpected, services =>
        {
            services.RemoveAll<ISourceFetcher>();
            services.AddSingleton<ISourceFetcher>(sources);
            services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddInMemoryExporter(spans));
        });
        var (fedId, brokenId) = await SeedAsync(worker);
        var runId = await StartRunAsync(worker);

        await worker.StartAsync();
        await worker.Time.TimerAsync(Pickup, Within);
        // A second pickup finds nothing to run; waiting for the one after it is the barrier for "it traces nothing".
        worker.Time.Advance(Pickup);
        await worker.Time.TimerAsync(Pickup, Within);
        await worker.StopAsync();

        // The activity source is static, so this exporter also hears other hosts' runs in this
        // process: a run is told by its id. A pickup that found nothing is exported by no host;
        // one that failed before finding its run is an error, and is not what this proves.
        var roots = spans.Snapshot().Where(span => span.DisplayName == "collection.run").ToList();
        Assert.DoesNotContain(roots, span => span.GetTagItem("keryx.run.id") is null && span.Status != ActivityStatusCode.Error);
        var root = Assert.Single(roots, span => $"{span.GetTagItem("keryx.run.id")}" == runId);
        Assert.Equal([("keryx.run.id", runId)], Tags(root));
        var trace = spans.Snapshot().Where(span => span.TraceId == root.TraceId).ToList();
        var stages = trace.Where(span => span.ParentSpanId == root.SpanId).ToList();
        Assert.Equal(
            ["collection.classify", "collection.deduplicate", "collection.fetch", "collection.parse", "collection.publish", "collection.store"],
            stages.Select(span => span.DisplayName).Order(StringComparer.Ordinal));
        Assert.All(stages, stage => Assert.Equal([("keryx.stage", stage.DisplayName["collection.".Length..])], Tags(stage)));
        var fetchStage = stages.Single(span => span.DisplayName == "collection.fetch");
        var fetches = trace.Where(span => span.ParentSpanId == fetchStage.SpanId).ToList();
        Assert.All(fetches, fetch => Assert.Equal("source.fetch", fetch.DisplayName));
        Assert.All(fetches, fetch => Assert.Equal(["keryx.source.id"], Tags(fetch).Select(tag => tag.Key)));
        Assert.Equal(
            new[] { brokenId, fedId }.Order(StringComparer.Ordinal),
            fetches.Select(fetch => Tags(fetch)[0].Value).Order(StringComparer.Ordinal));
        Assert.Equal(1 + 6 + 2, trace.Count);
        Assert.DoesNotContain(trace, span => Tags(span).Any(tag => tag.Value.Contains(Secret, StringComparison.Ordinal) || tag.Value.Contains("fit", StringComparison.Ordinal)));
    }

    // L2-094 AC3 with L2-019 AC3: a run that throws ends its span as an error, by type only, and
    // the run's correlation id is the id of its trace.
    [Fact]
    public async Task Given_a_retention_run_that_throws_When_it_is_traced_Then_its_span_is_an_error_and_the_runs_correlation_id_is_its_trace_id()
    {
        using var factory = new FaultFactory();
        await StaffClient.SeedAsync(factory);
        await using (var store = TestStore.Open(factory.Store))
        {
            store.Articles.Add(Article.Collect(
                new ArticleFacts("https://example.org/old", "Old", "All welcome.", "events", "toronto", [], null, 0.70m, PublicationStatus.Published),
                null, 1, "Example feed", factory.Time.GetUtcNow().AddYears(-3)));
            await store.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        factory.Fault.ArticleDeletesBeforeFailing = 0;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ApplyRetentionCommand(RunTrigger.Schedule), TestContext.Current.CancellationToken);
        }

        await using (var store = TestStore.Open(factory.Store))
        {
            var run = await store.Runs.SingleAsync(candidate => candidate.Worker == RunWorker.Retention, TestContext.Current.CancellationToken);
            var root = await RootAsync(factory, run);
            Assert.Equal("retention.run", root.DisplayName);
            Assert.Equal(RunOutcome.Failed, run.Outcome);
            Assert.Equal(root.TraceId.ToHexString(), run.CorrelationId);
            Assert.Equal([("error.type", typeof(DbUpdateException).FullName!), ("keryx.run.id", run.PublicId)], Tags(root));
            Assert.Equal((ActivityStatusCode.Error, null), (root.Status, root.StatusDescription));
        }
    }

    // L2-094 AC3: the daily digest is a run, traced by its id like the others.
    [Fact]
    public async Task Given_the_daily_digest_When_it_runs_Then_its_span_names_its_run()
    {
        using var factory = new FaultFactory();
        await StaffClient.SeedAsync(factory);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new SendHeldArticlesDigestCommand(), TestContext.Current.CancellationToken);
        }

        await using var store = TestStore.Open(factory.Store);
        var run = await store.Runs.SingleAsync(candidate => candidate.Worker == RunWorker.AlertDigest, TestContext.Current.CancellationToken);
        var root = await RootAsync(factory, run);
        Assert.Equal("alert_digest.run", root.DisplayName);
        Assert.Equal([("keryx.run.id", run.PublicId)], Tags(root));
    }

    private sealed class FaultFactory : ApiFactory
    {
        public SaveFault.Switch Fault { get; } = new();

        public ExportedCollection<Activity> Spans { get; } = new();

        protected override void ConfigureServices(IServiceCollection services)
        {
            services.AddScoped<IAppDbContext>(provider => new SaveFault(provider.GetRequiredService<KeryxDbContext>(), Fault));
            services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddInMemoryExporter(Spans));
        }
    }

    /// <summary>The root span of this host's run, told from other hosts' by the run's id.</summary>
    private static Task<Activity> RootAsync(FaultFactory factory, Run run) =>
        factory.Spans.WaitForAsync(
            span => span.ParentSpanId == default && $"{span.GetTagItem("keryx.run.id")}" == run.PublicId,
            span => $"{span.DisplayName} {span.GetTagItem("keryx.run.id")}",
            $"the root span of run {run.PublicId}");

    private static List<(string Key, string Value)> Tags(Activity span) =>
        [.. span.TagObjects.Select(tag => (tag.Key, $"{tag.Value}")).OrderBy(tag => tag.Key, StringComparer.Ordinal)];

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
}
