using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using Keryx.Application.Collection;
using Keryx.Application.Sources;
using Keryx.Domain.Collection;
using Keryx.Domain.Sources;
using Keryx.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Keryx.Api.Tests.Runs;

// Acceptance Test
// Traces to: L2-019 AC3, AC5
// Description: A run that throws is recorded failed with a generic reason and a correlation id,
// and frees the lease so the next slot starts a run; a run left running when the worker stopped
// is recorded failed with reason interrupted before any new run.
public sealed class RunRecordingTests
{
    private const string Toronto = "https://lifepointfit.ca/feed";

    [Fact]
    public async Task Given_the_pipeline_throws_When_the_run_executes_Then_it_is_failed_with_a_generic_reason_and_a_correlation_id_and_the_lease_is_free()
    {
        using var factory = new BrokenParserFactory();
        await StaffClient.SeedAsync(factory);
        factory.Sources.AnswerFeed(Toronto, "<rss version=\"2.0\"><channel><title>Feed</title></channel></rss>");
        await AddSourceAsync(factory);
        var runId = await StartRunAsync(factory);

        await SendAsync(factory, new RunCollectionCommand());

        using var client = await StaffClient.OpenAsync(factory);
        var run = await client.GetFromJsonAsync<JsonElement>(new Uri($"/admin/v1/runs/{runId}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal("failed", run.GetProperty("outcome").GetString());
        Assert.Equal("The run stopped unexpectedly.", run.GetProperty("failure_reason").GetString());
        var correlation = run.GetProperty("correlation_id").GetString();
        Assert.Matches("^[0-9a-f]{32}$", correlation);
        Assert.Equal(JsonValueKind.Number, run.GetProperty("duration_ms").ValueKind);
        // The log names the exception's type and the correlation id, never its message.
        var logged = Assert.Single(factory.Logs.Lines, line => line.Level == LogLevel.Error && line.Message.Contains(correlation!, StringComparison.Ordinal));
        Assert.Contains("InvalidOperationException", logged.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("parser broke", logged.Message, StringComparison.Ordinal);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Null((await scope.ServiceProvider.GetRequiredService<KeryxDbContext>().RunLeases.SingleAsync(TestContext.Current.CancellationToken)).ActiveRunId);
    }

    [Fact]
    public async Task Given_a_run_was_executing_when_the_worker_stopped_When_it_recovers_Then_that_run_is_failed_interrupted_and_others_are_untouched()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        var interrupted = await StartRunAsync(factory, executing: true);
        string pending, retention;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
            // Not picked up yet: the pipeline will run it.
            var waiting = Run.Start(RunWorker.Collection, RunTrigger.Manual, factory.Time.GetUtcNow());
            // Another worker's run: that worker recovers its own.
            var other = Run.Start(RunWorker.Retention, RunTrigger.Schedule, factory.Time.GetUtcNow());
            other.BeginExecution(0);
            db.Runs.AddRange(waiting, other);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            (pending, retention) = (waiting.PublicId, other.PublicId);
        }

        factory.Time.Advance(TimeSpan.FromMinutes(5));
        await SendAsync(factory, new RecoverInterruptedRuns(RunWorker.Collection));

        using var client = await StaffClient.OpenAsync(factory);
        var run = await client.GetFromJsonAsync<JsonElement>(new Uri($"/admin/v1/runs/{interrupted}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal("failed", run.GetProperty("outcome").GetString());
        Assert.Equal("interrupted", run.GetProperty("failure_reason").GetString());
        Assert.Equal(300_000, run.GetProperty("duration_ms").GetInt64());
        foreach (var untouched in new[] { pending, retention })
        {
            var other = await client.GetFromJsonAsync<JsonElement>(new Uri($"/admin/v1/runs/{untouched}", UriKind.Relative), TestContext.Current.CancellationToken);
            Assert.Equal("running", other.GetProperty("outcome").GetString());
        }
        await using var check = factory.Services.CreateAsyncScope();
        Assert.Null((await check.ServiceProvider.GetRequiredService<KeryxDbContext>().RunLeases.SingleAsync(TestContext.Current.CancellationToken)).ActiveRunId);
    }

    private static async Task AddSourceAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
        db.Sources.Add(Source.Add(SourceKind.Rss, Toronto, "LifePoint Fitness Ministry", "toronto", factory.Time.GetUtcNow()));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<string> StartRunAsync(ApiFactory factory, bool executing = false)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
        var run = Run.Start(RunWorker.Collection, RunTrigger.Schedule, factory.Time.GetUtcNow());
        if (executing)
        {
            run.BeginExecution(1);
        }

        db.Runs.Add(run);
        var lease = RunLease.For(RunWorker.Collection, factory.Time.GetUtcNow());
        lease.Claim(run.PublicId);
        db.RunLeases.Add(lease);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return run.PublicId;
    }

    private static async Task SendAsync(ApiFactory factory, IRequest request)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request, TestContext.Current.CancellationToken);
    }

    private sealed class BrokenParserFactory : ApiFactory
    {
        protected override void ConfigureServices(IServiceCollection services)
        {
            services.RemoveAll<IFeedParser>();
            services.AddSingleton<IFeedParser, BrokenParser>();
        }
    }

    private sealed class BrokenParser : IFeedParser
    {
        public ParsedFeed? Parse(SourceKind kind, string content) => throw new InvalidOperationException("The parser broke at 10.0.0.7.");
    }
}
