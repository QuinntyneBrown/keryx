using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using Keryx.Domain.Collection;
using Keryx.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Keryx.Api.Tests.Runs;

// Acceptance Test
// Traces to: L2-007, L2-019
// Description: Staff read one run of a worker, every count a number, with its six stage results.
public sealed class GetRunTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly string[] RunKeys =
    [
        "id", "worker", "trigger", "started_at", "finished_at", "duration_ms", "outcome", "failure_reason", "correlation_id",
        "added", "updated", "unchanged", "duplicates", "held", "off_topic", "rejected", "sources_ok", "sources_failed",
        "sources_checked", "sources_total", "stages", "issues",
    ];

    private static readonly string[] Stages = ["fetch", "parse", "classify", "deduplicate", "store", "publish"];

    [Fact]
    public async Task Given_the_collection_run_of_the_mocks_When_it_is_read_Then_every_count_is_a_number()
    {
        var started = factory.Time.GetUtcNow();
        var run = Run.Start(RunWorker.Collection, RunTrigger.Schedule, started);
        run.BeginExecution(64);
        for (var i = 0; i < 64; i++)
        {
            run.SourceChecked();
        }

        run.RecordStage(PipelineStage.Fetch, StageStatus.DoneWithWarnings, 41_200, new Dictionary<string, int> { ["sources_ok"] = 62, ["sources_failed"] = 2 });
        run.RecordStage(PipelineStage.Parse, StageStatus.Done, 3_100, new Dictionary<string, int> { ["items"] = 132 });
        run.RecordStage(PipelineStage.Classify, StageStatus.Done, 900, new Dictionary<string, int> { ["relevant"] = 70, ["held"] = 0, ["off_topic"] = 62 });
        run.RecordStage(PipelineStage.Deduplicate, StageStatus.Done, 400, new Dictionary<string, int> { ["duplicates"] = 18, ["rejected"] = 0 });
        run.RecordStage(PipelineStage.Store, StageStatus.Done, 700, new Dictionary<string, int> { ["added"] = 47, ["updated"] = 5 });
        run.RecordStage(PipelineStage.Publish, StageStatus.Done, 200, new Dictionary<string, int> { ["published"] = 52 });
        run.Finish(new RunCounts { Added = 47, Updated = 5, Unchanged = 0, Duplicates = 18, OffTopic = 62, SourcesOk = 62, SourcesFailed = 2 }, started.AddSeconds(48.5));
        await SaveAsync(run);
        using var client = await OpenAsync();

        var body = await client.GetFromJsonAsync<JsonElement>(new Uri($"/admin/v1/runs/{run.PublicId}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(RunKeys, body.EnumerateObject().Select(property => property.Name));
        Assert.Equal(run.PublicId, body.GetProperty("id").GetString());
        Assert.Equal("collection", body.GetProperty("worker").GetString());
        Assert.Equal("schedule", body.GetProperty("trigger").GetString());
        Assert.Equal("completed_with_issues", body.GetProperty("outcome").GetString());
        Assert.Equal(started, body.GetProperty("started_at").GetDateTimeOffset());
        Assert.Equal(started.AddSeconds(48.5), body.GetProperty("finished_at").GetDateTimeOffset());
        Assert.Equal(48_500, body.GetProperty("duration_ms").GetInt64());
        Assert.Equal(47, body.GetProperty("added").GetInt32());
        Assert.Equal(5, body.GetProperty("updated").GetInt32());
        Assert.Equal(18, body.GetProperty("duplicates").GetInt32());
        Assert.Equal(62, body.GetProperty("off_topic").GetInt32());
        Assert.Equal(62, body.GetProperty("sources_ok").GetInt32());
        Assert.Equal(2, body.GetProperty("sources_failed").GetInt32());
        Assert.Equal(64, body.GetProperty("sources_checked").GetInt32());
        Assert.Equal(64, body.GetProperty("sources_total").GetInt32());
        foreach (var name in new[] { "unchanged", "held", "rejected" })
        {
            Assert.Equal(0, body.GetProperty(name).GetInt32());
        }

        var stages = body.GetProperty("stages").EnumerateArray().ToList();
        Assert.Equal(Stages, stages.Select(stage => stage.GetProperty("stage").GetString()));
        Assert.Equal(["stage", "status", "duration_ms", "counts", "reason"], stages[0].EnumerateObject().Select(property => property.Name));
        Assert.Equal("done_with_warnings", stages[0].GetProperty("status").GetString());
        Assert.Equal(41_200, stages[0].GetProperty("duration_ms").GetInt64());
        Assert.Equal(62, stages[0].GetProperty("counts").GetProperty("sources_ok").GetInt32());
        Assert.Equal(2, stages[0].GetProperty("counts").GetProperty("sources_failed").GetInt32());
        Assert.All(stages.Skip(1), stage => Assert.Equal("done", stage.GetProperty("status").GetString()));
        Assert.Equal(JsonValueKind.Null, stages[0].GetProperty("reason").ValueKind);
        Assert.Empty(body.GetProperty("issues").EnumerateArray());
    }

    [Fact]
    public async Task Given_a_run_in_progress_When_it_is_read_Then_it_is_running_with_no_finish_and_its_progress_as_numbers()
    {
        var run = Run.Start(RunWorker.Collection, RunTrigger.Manual, factory.Time.GetUtcNow());
        run.BeginExecution(64);
        for (var i = 0; i < 9; i++)
        {
            run.SourceChecked();
        }

        await SaveAsync(run);
        using var client = await OpenAsync();

        var body = await client.GetFromJsonAsync<JsonElement>(new Uri($"/admin/v1/runs/{run.PublicId}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal("running", body.GetProperty("outcome").GetString());
        Assert.Equal("manual", body.GetProperty("trigger").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("finished_at").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("duration_ms").ValueKind);
        Assert.Equal(9, body.GetProperty("sources_checked").GetInt32());
        Assert.Equal(64, body.GetProperty("sources_total").GetInt32());
        Assert.All(body.GetProperty("stages").EnumerateArray(), stage =>
        {
            Assert.Equal("not_run", stage.GetProperty("status").GetString());
            Assert.Equal(0, stage.GetProperty("duration_ms").GetInt64());
            Assert.Empty(stage.GetProperty("counts").EnumerateObject());
        });
    }

    [Theory]
    [InlineData("run_aaaaaaaaaaaa")]
    [InlineData("not-a-run")]
    public async Task Given_no_run_has_the_id_When_it_is_read_Then_it_is_404_run_not_found(string id)
    {
        using var client = await OpenAsync();

        using var response = await client.GetAsync(new Uri($"/admin/v1/runs/{id}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("run_not_found", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Given_no_staff_session_When_a_run_is_read_Then_it_is_401()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/admin/v1/runs/run_aaaaaaaaaaaa", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<HttpClient> OpenAsync()
    {
        await StaffClient.SeedAsync(factory);
        return await StaffClient.OpenAsync(factory);
    }

    private async Task SaveAsync(Run run)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
        db.Runs.Add(run);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
