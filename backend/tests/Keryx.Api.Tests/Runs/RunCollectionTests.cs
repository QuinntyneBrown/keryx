using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using Keryx.Application.Collection;
using Keryx.Domain.Collection;
using Keryx.Domain.Sources;
using Keryx.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Keryx.Api.Tests.Runs;

// Acceptance Test
// Traces to: L2-004, L2-007 AC1 AC2 AC4, L2-008 AC1, L2-019 AC2
// Description: A collection run fetches every source it can read, isolates a source that fails,
// parses and classifies the items of the others, records each stage, and updates each source's
// health. It runs the worker's handler directly against fake sources; each test has its own store.
public sealed class RunCollectionTests
{
    private const string Toronto = "https://lifepointfit.ca/feed";
    private const string Global = "https://worldfaithfit.org/feed";
    private const string Throwing = "https://throws.example/events";
    private const string Gone = "https://gone.example/feed";

    [Fact]
    public async Task Given_two_of_four_sources_fail_When_a_run_executes_Then_the_others_items_are_classified_and_the_run_completed_with_issues()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        // Toronto is a focus area. "running" and "church": 0.6 × 2/3 + 0.4 = 0.80, published.
        // No topic: 0.3, hidden. In the global region "running" and "faith": 0.40 + 0.08 = 0.48, held.
        factory.Sources.AnswerFeed(Toronto, Feed(("Sunrise running club at St. Paul's church", "https://lifepointfit.ca/1"), ("Bake sale this Saturday", "https://lifepointfit.ca/2")));
        factory.Sources.AnswerFeed(Global, Feed(("Running and faith retreat", "https://worldfaithfit.org/1")));
        factory.Sources.Throw(Throwing);
        await AddSourcesAsync(factory,
            (SourceKind.Rss, Toronto, "LifePoint Fitness Ministry", "toronto"),
            (SourceKind.Rss, Global, "World Faith Fit", "global"),
            (SourceKind.Website, Throwing, "Broken Bootcamp", "toronto"),
            (SourceKind.Rss, Gone, "Gone Gym", "toronto"),
            // Read only through platform APIs that are not set up yet: not fetched, not counted.
            (SourceKind.Instagram, "https://www.instagram.com/lifepointfit/", "LifePoint on Instagram", "toronto"));
        var runId = await StartRunAsync(factory);

        await SendAsync(factory, new RunCollectionCommand());

        using var client = await StaffClient.OpenAsync(factory);
        var run = await client.GetFromJsonAsync<JsonElement>(new Uri($"/admin/v1/runs/{runId}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal("completed_with_issues", run.GetProperty("outcome").GetString());
        Assert.Equal(4, run.GetProperty("sources_total").GetInt32());
        Assert.Equal(4, run.GetProperty("sources_checked").GetInt32());
        Assert.Equal(2, run.GetProperty("sources_ok").GetInt32());
        Assert.Equal(2, run.GetProperty("sources_failed").GetInt32());
        Assert.Equal(1, run.GetProperty("held").GetInt32());
        Assert.Equal(1, run.GetProperty("off_topic").GetInt32());
        var stages = run.GetProperty("stages").EnumerateArray().ToDictionary(stage => stage.GetProperty("stage").GetString()!);
        Assert.Equal("done_with_warnings", stages["fetch"].GetProperty("status").GetString());
        Assert.Equal(2, stages["fetch"].GetProperty("counts").GetProperty("sources_ok").GetInt32());
        Assert.Equal(2, stages["fetch"].GetProperty("counts").GetProperty("sources_failed").GetInt32());
        Assert.Equal("done", stages["parse"].GetProperty("status").GetString());
        Assert.Equal(3, stages["parse"].GetProperty("counts").GetProperty("items").GetInt32());
        // L2-007 AC4: held items make classify done_with_warnings, and its counts sum to the items parsed.
        Assert.Equal("done_with_warnings", stages["classify"].GetProperty("status").GetString());
        var classify = stages["classify"].GetProperty("counts");
        Assert.Equal((1, 1, 1), (classify.GetProperty("relevant").GetInt32(), classify.GetProperty("held").GetInt32(), classify.GetProperty("off_topic").GetInt32()));
        Assert.All(stages.Values, stage => Assert.Equal(JsonValueKind.Number, stage.GetProperty("duration_ms").ValueKind));
        Assert.DoesNotContain(factory.Sources.Requests, request => request.Address.Host.Contains("instagram", StringComparison.Ordinal));
        Assert.All(factory.Sources.Requests, request => Assert.Equal(TimeSpan.FromSeconds(30), request.Timeout));

        // L2-004: health follows each source's fetch.
        var sources = await client.GetFromJsonAsync<JsonElement>(new Uri("/admin/v1/sources?page_size=100", UriKind.Relative), TestContext.Current.CancellationToken);
        var byName = sources.GetProperty("items").EnumerateArray().ToDictionary(item => item.GetProperty("organization_name").GetString()!);
        Assert.Equal(("healthy", 0), Health(byName["LifePoint Fitness Ministry"]));
        Assert.Equal(("needs_look", 0), Health(byName["World Faith Fit"]));
        Assert.Equal(1, byName["World Faith Fit"].GetProperty("held_count").GetInt32());
        Assert.Equal(("failing", 1), Health(byName["Broken Bootcamp"]));
        Assert.Equal("refused", byName["Broken Bootcamp"].GetProperty("failure_reason").GetString());
        Assert.Equal(("failing", 1), Health(byName["Gone Gym"]));
        Assert.Equal("not_found", byName["Gone Gym"].GetProperty("failure_reason").GetString());
        Assert.Equal(JsonValueKind.Null, byName["LifePoint Fitness Ministry"].GetProperty("failure_reason").ValueKind);
        Assert.Equal(factory.Time.GetUtcNow(), byName["Gone Gym"].GetProperty("last_fetched_at").GetDateTimeOffset());
        Assert.Equal(JsonValueKind.Null, byName["LifePoint on Instagram"].GetProperty("last_fetched_at").ValueKind);
    }

    [Fact]
    public async Task Given_every_source_answers_When_a_run_executes_Then_all_six_stages_are_done_and_the_run_succeeded_and_the_lease_is_free()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        factory.Sources.AnswerFeed(Toronto, Feed(("Sunrise running club at St. Paul's church", "https://lifepointfit.ca/1")));
        await AddSourcesAsync(factory, (SourceKind.Rss, Toronto, "LifePoint Fitness Ministry", "toronto"));
        var runId = await StartRunAsync(factory);

        await SendAsync(factory, new RunCollectionCommand());
        // A second pickup finds no pending run and changes nothing.
        await SendAsync(factory, new RunCollectionCommand());

        using var client = await StaffClient.OpenAsync(factory);
        var run = await client.GetFromJsonAsync<JsonElement>(new Uri($"/admin/v1/runs/{runId}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal("succeeded", run.GetProperty("outcome").GetString());
        Assert.All(run.GetProperty("stages").EnumerateArray(), stage => Assert.Equal("done", stage.GetProperty("status").GetString()));
        Assert.Equal(JsonValueKind.Number, run.GetProperty("duration_ms").ValueKind);
        Assert.Single(factory.Sources.Requests);
        await using var scope = factory.Services.CreateAsyncScope();
        var lease = await scope.ServiceProvider.GetRequiredService<KeryxDbContext>().RunLeases.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Null(lease.ActiveRunId);
    }

    [Fact]
    public async Task Given_the_pipeline_already_started_the_leased_run_When_a_pickup_comes_Then_it_does_not_run_it_again()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        factory.Sources.AnswerFeed(Toronto, Feed(("Sunrise running club at St. Paul's church", "https://lifepointfit.ca/1")));
        await AddSourcesAsync(factory, (SourceKind.Rss, Toronto, "LifePoint Fitness Ministry", "toronto"));
        var runId = await StartRunAsync(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
            (await db.Runs.SingleAsync(run => run.PublicId == runId, TestContext.Current.CancellationToken)).BeginExecution(1);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await SendAsync(factory, new RunCollectionCommand());

        Assert.Empty(factory.Sources.Requests);
        using var client = await StaffClient.OpenAsync(factory);
        var run = await client.GetFromJsonAsync<JsonElement>(new Uri($"/admin/v1/runs/{runId}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal("running", run.GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task Given_a_source_failed_last_run_When_it_answers_Then_it_is_healthy_again_with_no_failures()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await AddSourcesAsync(factory, (SourceKind.Rss, Toronto, "LifePoint Fitness Ministry", "toronto"));
        await StartRunAsync(factory);
        await SendAsync(factory, new RunCollectionCommand());

        factory.Sources.AnswerFeed(Toronto, Feed(("Sunrise running club at St. Paul's church", "https://lifepointfit.ca/1")));
        await StartRunAsync(factory);
        await SendAsync(factory, new RunCollectionCommand());

        using var client = await StaffClient.OpenAsync(factory);
        var sources = await client.GetFromJsonAsync<JsonElement>(new Uri("/admin/v1/sources", UriKind.Relative), TestContext.Current.CancellationToken);
        var source = sources.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("organization_name").GetString() == "LifePoint Fitness Ministry");
        Assert.Equal(("healthy", 0), Health(source));
        Assert.Equal(JsonValueKind.Null, source.GetProperty("failure_reason").ValueKind);
    }

    private static (string, int) Health(JsonElement source) =>
        (source.GetProperty("health").GetString()!, source.GetProperty("consecutive_failures").GetInt32());

    private static string Feed(params (string Title, string Link)[] items) =>
        "<rss version=\"2.0\"><channel><title>Feed</title>" +
        string.Concat(items.Select(item => $"<item><title>{item.Title}</title><link>{item.Link}</link><description>{item.Title}</description></item>")) +
        "</channel></rss>";

    private static async Task AddSourcesAsync(ApiFactory factory, params (SourceKind Kind, string Address, string Name, string Region)[] sources)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
        foreach (var (kind, address, name, region) in sources)
        {
            db.Sources.Add(Source.Add(kind, address, name, region, factory.Time.GetUtcNow()));
        }

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>What a schedule slot does: a pending run, named by the collection lease.</summary>
    private static async Task<string> StartRunAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
        var run = Run.Start(RunWorker.Collection, RunTrigger.Schedule, factory.Time.GetUtcNow());
        db.Runs.Add(run);
        var lease = await db.RunLeases.SingleOrDefaultAsync(TestContext.Current.CancellationToken);
        if (lease is null)
        {
            lease = RunLease.For(RunWorker.Collection, factory.Time.GetUtcNow());
            db.RunLeases.Add(lease);
        }

        lease.Claim(run.PublicId);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return run.PublicId;
    }

    private static async Task SendAsync(ApiFactory factory, IRequest request)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request, TestContext.Current.CancellationToken);
    }
}
