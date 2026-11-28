using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Application.Collection;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using static Keryx.Api.Tests.Runs.CollectionRuns;
using static Keryx.Api.Tests.Runs.RetryFailedSourcesTests;

namespace Keryx.Api.Tests.Runs;

// Acceptance Test
// Traces to: L2-008 AC3, AC4
// Description: The worker fetches only a retried run's failed sources, passes their items through
// the remaining stages and merges the results into that same run: its counts and fetch stage add
// up, a recovered source's issue is closed and leaves the run, and the outcome reads as if those
// sources had answered the first time.
public sealed class RunRetryTests
{
    private const string Redeemed = "https://redeemedrun.ca/feed";
    private const string Covenant = "https://covenantcycling.ca/feed";

    [Fact]
    public async Task Given_a_retry_in_which_one_of_two_failed_sources_answers_When_it_finishes_Then_the_same_run_has_1_failed_and_its_items_and_the_recovered_issue_is_closed()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        var runId = await RunWithFailuresAsync(factory);
        var before = await ReadRunAsync(client, runId);
        factory.Sources.AnswerFeed(Covenant, Feed(("Sunrise running club at St. Paul's church", "https://covenantcycling.ca/1", "Sunrise running club at St. Paul's church")));
        var fetchedBefore = factory.Sources.Requests.Count;

        await RetryAndExecuteAsync(factory, client, runId);

        // Only the two failed sources were fetched again.
        Assert.Equal([Covenant, Redeemed], factory.Sources.Requests.Skip(fetchedBefore).Select(request => request.Address.OriginalString).Order(StringComparer.Ordinal));
        var run = await ReadRunAsync(client, runId);
        Assert.Equal("completed_with_issues", run.GetProperty("outcome").GetString());
        Assert.Equal((4, 1), (run.GetProperty("sources_ok").GetInt32(), run.GetProperty("sources_failed").GetInt32()));
        Assert.Equal((5, 5), (run.GetProperty("sources_checked").GetInt32(), run.GetProperty("sources_total").GetInt32()));
        Assert.Equal(1, before.GetProperty("added").GetInt32());
        Assert.Equal(2, run.GetProperty("added").GetInt32());
        Assert.Equal(before.GetProperty("finished_at").GetDateTimeOffset(), run.GetProperty("finished_at").GetDateTimeOffset());
        var fetch = StageCounts(run, "fetch");
        Assert.Equal((4, 1), (fetch.GetProperty("sources_ok").GetInt32(), fetch.GetProperty("sources_failed").GetInt32()));
        Assert.Equal("done_with_warnings", Stage(run, "fetch").GetProperty("status").GetString());
        // The first pass held an item; the retry held none: classify still warns.
        Assert.Equal("done_with_warnings", Stage(run, "classify").GetProperty("status").GetString());
        Assert.Equal(StageCounts(before, "classify").GetProperty("relevant").GetInt32() + 1, StageCounts(run, "classify").GetProperty("relevant").GetInt32());
        Assert.Equal(["Redeemed Run Club Toronto", "World Faith Fit"], IssueSubjects(run));
        Assert.DoesNotContain("Covenant Cycling Collective", await OpenIssueSubjectsAsync(client));
        Assert.Equal(2, await CountCollectionRunsAsync(client));
        // The lease is free again: a run can start.
        using var start = await client.PostAsync(new Uri("/admin/v1/runs", UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
    }

    [Fact]
    public async Task Given_a_retry_in_which_no_failed_source_answers_When_it_finishes_Then_the_run_is_as_it_was_and_no_streak_grows()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        var runId = await RunWithFailuresAsync(factory);
        var before = await ReadRunAsync(client, runId);

        await RetryAndExecuteAsync(factory, client, runId);

        var run = await ReadRunAsync(client, runId);
        Assert.Equal("completed_with_issues", run.GetProperty("outcome").GetString());
        Assert.Equal((3, 2), (run.GetProperty("sources_ok").GetInt32(), run.GetProperty("sources_failed").GetInt32()));
        Assert.Equal(before.GetProperty("added").GetInt32(), run.GetProperty("added").GetInt32());
        Assert.Equal(Issues(before), Issues(run));
    }

    [Fact]
    public async Task Given_a_retry_in_which_every_failed_source_answers_When_it_finishes_Then_the_run_succeeded()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        var runId = await RunWithFailuresAsync(factory);
        factory.Sources.AnswerFeed(Covenant, Feed());
        factory.Sources.AnswerFeed(Redeemed, Feed());

        await RetryAndExecuteAsync(factory, client, runId);

        var run = await ReadRunAsync(client, runId);
        Assert.Equal("succeeded", run.GetProperty("outcome").GetString());
        Assert.Equal((5, 0), (run.GetProperty("sources_ok").GetInt32(), run.GetProperty("sources_failed").GetInt32()));
        Assert.Equal("done", Stage(run, "fetch").GetProperty("status").GetString());
        Assert.Equal(["World Faith Fit"], IssueSubjects(run));
    }

    private static async Task RetryAndExecuteAsync(ApiFactory factory, HttpClient client, string runId)
    {
        using (var response = await RetryAsync(client, runId))
        {
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunCollectionCommand(), TestContext.Current.CancellationToken);
    }

    private static JsonElement Stage(JsonElement run, string name) =>
        run.GetProperty("stages").EnumerateArray().Single(stage => stage.GetProperty("stage").GetString() == name);

    private static JsonElement StageCounts(JsonElement run, string name) => Stage(run, name).GetProperty("counts");

    private static string[] IssueSubjects(JsonElement run) =>
        [.. run.GetProperty("issues").EnumerateArray().Select(issue => issue.GetProperty("subject_name").GetString()!).Order(StringComparer.Ordinal)];

    /// <summary>Each issue as id, kind, detail and streak, in the run's order.</summary>
    private static string[] Issues(JsonElement run) =>
        [.. run.GetProperty("issues").EnumerateArray().Select(issue =>
            $"{issue.GetProperty("id").GetString()} {issue.GetProperty("kind").GetString()} {issue.GetProperty("detail").GetString()} {issue.GetProperty("consecutive_runs").GetInt32()}")];

    private static async Task<string[]> OpenIssueSubjectsAsync(HttpClient client)
    {
        var page = await client.GetFromJsonAsync<JsonElement>(new Uri("/admin/v1/issues?page_size=50", UriKind.Relative), TestContext.Current.CancellationToken);
        return [.. page.GetProperty("items").EnumerateArray().Select(issue => issue.GetProperty("subject_name").GetString() ?? string.Empty)];
    }

    private static async Task<int> CountCollectionRunsAsync(HttpClient client)
    {
        var page = await client.GetFromJsonAsync<JsonElement>(new Uri("/admin/v1/runs", UriKind.Relative), TestContext.Current.CancellationToken);
        return page.GetProperty("total").GetInt32();
    }
}
