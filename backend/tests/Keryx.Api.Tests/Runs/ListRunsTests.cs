using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using Keryx.Domain.Collection;
using Keryx.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Keryx.Api.Tests.Runs;

// Acceptance Test
// Traces to: L2-044 AC1–AC3 (the data); View run history design (GET /admin/v1/runs)
// Description: Staff list a worker's runs a page at a time, newest first, every count a number;
// problems=true narrows to runs that failed or completed with issues.
public sealed class ListRunsTests
{
    private static readonly string[] ItemKeys =
        ["added", "duration_ms", "finished_at", "id", "outcome", "sources_checked", "sources_failed", "sources_total", "started_at", "trigger"];

    [Fact]
    public async Task Given_16_collection_runs_and_a_retention_run_When_14_are_listed_Then_the_newest_14_collection_runs_come_newest_first()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        var start = factory.Time.GetUtcNow().AddDays(-4);
        var runs = Enumerable.Range(0, 16).Select(i => Finished(start.AddHours(6 * i), added: i)).ToList();
        await SaveAsync(factory, [.. runs, Run.Start(RunWorker.Retention, RunTrigger.Schedule, start.AddHours(200))]);

        var page = await client.GetFromJsonAsync<JsonElement>(new Uri("/admin/v1/runs?worker=collection&page=1&page_size=14", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal((16, 1, 14), (page.GetProperty("total").GetInt32(), page.GetProperty("page").GetInt32(), page.GetProperty("page_size").GetInt32()));
        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(Enumerable.Range(2, 14).Reverse(), items.Select(item => item.GetProperty("added").GetInt32()));
        var newest = items[0];
        Assert.Equal(ItemKeys, newest.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal((runs[15].PublicId, "schedule", "succeeded"), (newest.GetProperty("id").GetString(), newest.GetProperty("trigger").GetString(), newest.GetProperty("outcome").GetString()));
        Assert.Equal(240_000, newest.GetProperty("duration_ms").GetInt64());
        Assert.Equal(0, newest.GetProperty("sources_failed").GetInt32());
    }

    [Fact]
    public async Task Given_no_worker_When_runs_are_listed_Then_collection_runs_are_listed_in_pages_of_10()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        var start = factory.Time.GetUtcNow().AddDays(-4);
        await SaveAsync(factory, [.. Enumerable.Range(0, 12).Select(i => Finished(start.AddHours(6 * i), added: i)), Run.Start(RunWorker.Retention, RunTrigger.Schedule, start)]);

        var page = await client.GetFromJsonAsync<JsonElement>(new Uri("/admin/v1/runs?page=2", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal((12, 2, 10), (page.GetProperty("total").GetInt32(), page.GetProperty("page").GetInt32(), page.GetProperty("page_size").GetInt32()));
        Assert.Equal([1, 0], page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("added").GetInt32()));
    }

    [Fact]
    public async Task Given_runs_with_and_without_problems_When_problems_are_listed_Then_only_failed_and_completed_with_issues_runs_come()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        var start = factory.Time.GetUtcNow().AddDays(-1);
        var issues = Run.Start(RunWorker.Collection, RunTrigger.Schedule, start);
        issues.Finish(new RunCounts { Added = 3, SourcesOk = 62, SourcesFailed = 2 }, start.AddMinutes(4));
        var failed = Run.Start(RunWorker.Collection, RunTrigger.Manual, start.AddHours(6));
        failed.Fail("interrupted", null, start.AddHours(6).AddMinutes(1));
        var running = Run.Start(RunWorker.Collection, RunTrigger.Schedule, start.AddHours(18));
        await SaveAsync(factory, issues, Finished(start.AddHours(12), added: 9), failed, running);

        var page = await client.GetFromJsonAsync<JsonElement>(new Uri("/admin/v1/runs?problems=true", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal([failed.PublicId, issues.PublicId], page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetString()));
        var first = page.GetProperty("items")[0];
        Assert.Equal(("manual", "failed"), (first.GetProperty("trigger").GetString(), first.GetProperty("outcome").GetString()));
        Assert.Equal(2, page.GetProperty("items")[1].GetProperty("sources_failed").GetInt32());
        var all = await client.GetFromJsonAsync<JsonElement>(new Uri("/admin/v1/runs?problems=false", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(4, all.GetProperty("total").GetInt32());
        var newest = all.GetProperty("items")[0];
        Assert.Equal(("running", JsonValueKind.Null, JsonValueKind.Null), (newest.GetProperty("outcome").GetString(), newest.GetProperty("finished_at").ValueKind, newest.GetProperty("duration_ms").ValueKind));
    }

    [Fact]
    public async Task Given_runs_of_two_workers_When_retention_runs_are_listed_Then_only_they_come()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        var retention = Run.Start(RunWorker.Retention, RunTrigger.Schedule, factory.Time.GetUtcNow().AddHours(-2));
        await SaveAsync(factory, Finished(factory.Time.GetUtcNow().AddHours(-1), added: 5), retention);

        var page = await client.GetFromJsonAsync<JsonElement>(new Uri("/admin/v1/runs?worker=retention", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal([retention.PublicId], page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetString()));
    }

    [Theory]
    [InlineData("page=0", "invalid_page")]
    [InlineData("page_size=51", "invalid_page")]
    [InlineData("page_size=x", "invalid_page")]
    [InlineData("worker=payroll", "invalid_filter")]
    [InlineData("problems=yes", "invalid_filter")]
    public async Task Given_a_value_it_cannot_use_When_runs_are_listed_Then_400_with_its_code(string query, string code)
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);

        using var response = await client.GetAsync(new Uri($"/admin/v1/runs?{query}", UriKind.Relative), TestContext.Current.CancellationToken);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal((HttpStatusCode.BadRequest, code), (response.StatusCode, problem.GetProperty("code").GetString()));
    }

    // L2-058 AC1: a run in progress is listed with how far it has got, as numbers.
    [Fact]
    public async Task Given_a_run_in_progress_When_runs_are_listed_Then_it_comes_first_with_its_sources_checked_and_total()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        var now = factory.Time.GetUtcNow();
        var running = Run.Start(RunWorker.Collection, RunTrigger.Manual, now.AddSeconds(-92));
        running.BeginExecution(64);
        for (var i = 0; i < 38; i++)
        {
            running.SourceChecked();
        }

        await SaveAsync(factory, Finished(now.AddHours(-6), added: 4), running);

        var page = await client.GetFromJsonAsync<JsonElement>(new Uri("/admin/v1/runs?page_size=14", UriKind.Relative), TestContext.Current.CancellationToken);

        var items = page.GetProperty("items").EnumerateArray().ToList();
        var first = items[0];
        Assert.Equal((running.PublicId, "running", "manual"), (first.GetProperty("id").GetString(), first.GetProperty("outcome").GetString(), first.GetProperty("trigger").GetString()));
        Assert.Equal((38, 64), (first.GetProperty("sources_checked").GetInt32(), first.GetProperty("sources_total").GetInt32()));
        Assert.Equal(JsonValueKind.Null, first.GetProperty("duration_ms").ValueKind);
        Assert.Equal(JsonValueKind.Null, first.GetProperty("finished_at").ValueKind);
    }

    private static Run Finished(DateTimeOffset at, int added)
    {
        var run = Run.Start(RunWorker.Collection, RunTrigger.Schedule, at);
        run.Finish(new RunCounts { Added = added, SourcesOk = 64 }, at.AddMinutes(4));
        return run;
    }

    private static async Task<HttpClient> OpenAsync(ApiFactory factory)
    {
        await StaffClient.SeedAsync(factory);
        return await StaffClient.OpenAsync(factory);
    }

    private static async Task SaveAsync(ApiFactory factory, params Run[] runs)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
        db.Runs.AddRange(runs);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
