using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using Keryx.Application.Sources;
using Keryx.Domain.Collection;
using Keryx.Domain.Sources;
using Keryx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Keryx.Api.Tests.Runs.CollectionRuns;

namespace Keryx.Api.Tests.Runs;

// Acceptance Test
// Traces to: L2-008 AC3 (the request), AC5; L2-059 AC5
// Description: Staff ask for a finished run's failed sources to be fetched again. The same run is
// marked running for its failed sources only, and no run is made; a run that failed at a stage
// starts a new manual run instead. A retry is refused while another run is in progress, for an
// unknown run, and for a run with no failed source.
public sealed class RetryFailedSourcesTests
{
    private static readonly string[] RetryKeys = ["id"];

    [Fact]
    public async Task Given_a_run_with_2_failed_sources_When_staff_retry_Then_202_with_the_same_run_running_for_those_2_and_no_run_is_made()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        var runId = await RunWithFailuresAsync(factory);

        using var response = await RetryAsync(client, runId);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var retry = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(RetryKeys, retry.EnumerateObject().Select(property => property.Name));
        Assert.Equal(runId, retry.GetProperty("id").GetString());
        var run = await ReadRunAsync(client, runId);
        Assert.Equal("running", run.GetProperty("outcome").GetString());
        Assert.Equal((0, 2), (run.GetProperty("sources_checked").GetInt32(), run.GetProperty("sources_total").GetInt32()));
        // The two runs that made it, and no third.
        Assert.Equal(2, await CountRunsAsync(factory));
        // The retry holds the lease: a start meanwhile is refused with the retried run.
        using var start = await client.PostAsync(new Uri("/admin/v1/runs", UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, start.StatusCode);
        Assert.Equal(runId, (await start.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("run_id").GetString());
    }

    [Fact]
    public async Task Given_a_run_that_failed_at_a_stage_When_staff_retry_Then_202_with_a_new_manual_run()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        var failed = Run.Start(RunWorker.Collection, RunTrigger.Schedule, factory.Time.GetUtcNow().AddMinutes(-30));
        failed.FailStage(PipelineStage.Store, "The records of this run could not be saved.", factory.Time.GetUtcNow().AddMinutes(-26));
        await SaveRunsAsync(factory, failed);

        using var response = await RetryAsync(client, failed.PublicId);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetString()!;
        Assert.NotEqual(failed.PublicId, id);
        var run = await ReadRunAsync(client, id);
        Assert.Equal(("manual", "running"), (run.GetProperty("trigger").GetString(), run.GetProperty("outcome").GetString()));
        Assert.Equal("failed", (await ReadRunAsync(client, failed.PublicId)).GetProperty("outcome").GetString());
        Assert.Equal(2, await CountRunsAsync(factory));
    }

    [Fact]
    public async Task Given_another_run_in_progress_When_staff_retry_Then_409_run_in_progress_with_its_id_and_the_run_is_unchanged()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        var runId = await RunWithFailuresAsync(factory);
        var running = await StartRunTests.StartAsync(client);

        using var response = await RetryAsync(client, runId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("run_in_progress", problem.GetProperty("code").GetString());
        Assert.Equal(running, problem.GetProperty("run_id").GetString());
        Assert.Equal("completed_with_issues", (await ReadRunAsync(client, runId)).GetProperty("outcome").GetString());
    }

    [Theory]
    [InlineData("run_unknown")]
    [InlineData("not-a-run")]
    public async Task Given_no_such_run_When_staff_retry_Then_404_run_not_found(string id)
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);

        using var response = await RetryAsync(client, id);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("run_not_found", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Given_a_run_with_no_failed_source_When_staff_retry_Then_409_nothing_to_retry_and_it_is_unchanged()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, "https://templefit.ca/feed", "Temple Fit", "toronto");
        factory.Sources.AnswerFeed("https://templefit.ca/feed", Feed());
        var runId = await RunAsync(factory);

        using var response = await RetryAsync(client, runId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("nothing_to_retry", problem.GetProperty("code").GetString());
        Assert.Equal("succeeded", (await ReadRunAsync(client, runId)).GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task Given_an_earlier_run_When_staff_retry_it_Then_only_its_sources_still_failing_are_counted()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, "https://redeemedrun.ca/feed", "Redeemed Run Club Toronto", "toronto");
        await AddSourceAsync(factory, SourceKind.Rss, "https://kingdomlifters.ca/feed", "Kingdom Lifters", "toronto");
        await AddSourceAsync(factory, SourceKind.Rss, "https://covenantcycling.ca/feed", "Covenant Cycling Collective", "toronto");
        factory.Sources.AnswerFeed("https://kingdomlifters.ca/feed", Feed());
        var earlier = await RunAsync(factory);
        // In the next run Kingdom Lifters starts failing, which is not the earlier run's failure,
        // and Covenant Cycling recovers, closing its issue: only Redeemed Run is left to retry.
        factory.Sources.Answer("https://kingdomlifters.ca/feed", SourceFetchResult.Failed(FetchFailureReason.Timeout));
        factory.Sources.AnswerFeed("https://covenantcycling.ca/feed", Feed());
        await RunAsync(factory);

        using var response = await RetryAsync(client, earlier);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(1, (await ReadRunAsync(client, earlier)).GetProperty("sources_total").GetInt32());
    }

    [Fact]
    public async Task Given_a_failed_retention_run_When_staff_retry_Then_409_nothing_to_retry_and_no_run_starts()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        var retention = Run.Start(RunWorker.Retention, RunTrigger.Schedule, factory.Time.GetUtcNow().AddMinutes(-30));
        retention.Fail("The run stopped unexpectedly.", correlationId: null, factory.Time.GetUtcNow().AddMinutes(-29));
        await SaveRunsAsync(factory, retention);

        using var response = await RetryAsync(client, retention.PublicId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("nothing_to_retry", problem.GetProperty("code").GetString());
        Assert.Equal(1, await CountRunsAsync(factory));
    }

    /// <summary>
    /// A finished run in which Temple Fit answered and two sources failed; an earlier run's
    /// failure of a third source, since recovered, is not this run's to retry.
    /// </summary>
    internal static async Task<string> RunWithFailuresAsync(ApiFactory factory)
    {
        await AddSourceAsync(factory, SourceKind.Rss, "https://templefit.ca/feed", "Temple Fit", "toronto");
        await AddSourceAsync(factory, SourceKind.Rss, "https://redeemedrun.ca/feed", "Redeemed Run Club Toronto", "toronto");
        await AddSourceAsync(factory, SourceKind.Rss, "https://covenantcycling.ca/feed", "Covenant Cycling Collective", "toronto");
        await AddSourceAsync(factory, SourceKind.Rss, "https://kingdomlifters.ca/feed", "Kingdom Lifters", "toronto");
        factory.Sources.AnswerFeed("https://templefit.ca/feed", Feed());
        // World Faith Fit answers with an item held for review: a warning, not a failure.
        await AddSourceAsync(factory, SourceKind.Rss, "https://worldfaithfit.org/feed", "World Faith Fit", "global");
        factory.Sources.AnswerFeed("https://worldfaithfit.org/feed",
            "<rss version=\"2.0\"><channel><title>Feed</title><item><title>Running and faith retreat</title><link>https://worldfaithfit.org/1</link></item></channel></rss>");
        // Kingdom Lifters fails in the first run only.
        await RunAsync(factory);
        factory.Sources.AnswerFeed("https://kingdomlifters.ca/feed", Feed());
        return await RunAsync(factory);
    }

    internal static Task<HttpResponseMessage> RetryAsync(HttpClient client, string runId) =>
        client.PostAsync(new Uri($"/admin/v1/runs/{runId}/retry", UriKind.Relative), content: null, TestContext.Current.CancellationToken);

    internal static async Task<HttpClient> OpenAsync(ApiFactory factory)
    {
        await StaffClient.SeedAsync(factory);
        return await StaffClient.OpenAsync(factory);
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
}
