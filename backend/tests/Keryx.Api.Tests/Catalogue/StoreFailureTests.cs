using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Fakes;
using Keryx.Api.Tests.Session;
using Keryx.Application.Abstractions;
using Keryx.Domain.Sources;
using Keryx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Keryx.Api.Tests.Runs.CollectionRuns;

namespace Keryx.Api.Tests.Catalogue;

// Acceptance Test
// Traces to: L2-007 AC3, AC5
// Description: When the store stage cannot reach the database, the stage fails with a generic
// reason, the later stage is not run, the run fails, its staged records are discarded, and the API
// serves exactly what it served before the run began.
public sealed class StoreFailureTests
{
    private const string Address = "https://redeemedrun.ca/feed";

    [Fact]
    public async Task Given_the_store_stage_cannot_reach_the_database_When_the_run_stops_Then_it_failed_and_the_api_serves_what_it_served_before()
    {
        using var factory = new Factory();
        await StaffClient.SeedAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, Address, "Redeemed Run Club", "toronto");
        await AddSourceAsync(factory, SourceKind.Rss, "https://lifepointfit.ca/feed", "LifePoint Fitness", "toronto");
        factory.Sources.AnswerFeed(Address, Feed(("Toronto church running club starts spring 5K training", "https://redeemedrun.ca/news/5k", "Weekly runs.")));
        await RunAsync(factory);
        var id = await ArticleIdAsync(factory);
        string before;
        using (var earlier = await StaffClient.OpenAsync(factory))
        {
            before = (await ReadArticleAsync(earlier, id)).GetRawText();
        }

        factory.Time.Advance(TimeSpan.FromHours(6));
        factory.Sources.AnswerFeed(Address, Feed(
            ("Toronto church running club starts spring 10K training", "https://redeemedrun.ca/news/5k", "Weekly runs."),
            ("Sunrise prayer and fitness bootcamp in Toronto", "https://redeemedrun.ca/news/bootcamp", "Before work.")));
        factory.Sources.AnswerFeed("https://lifepointfit.ca/feed", Feed(("Faith and fitness night for Toronto youth", "https://lifepointfit.ca/e/1", "Games.")));
        await AddSourceAsync(factory, SourceKind.Rss, "https://gracechurch.ca/feed", "Grace Church", "toronto");
        factory.Sources.AnswerFeed("https://gracechurch.ca/feed", Feed(("Grace Church opens a Toronto fitness ministry", "https://gracechurch.ca/n/1", "Weekly.")));
        // One source's records are staged and saved; the next source's save cannot reach the database.
        factory.Fault.StoreArmed = true;
        factory.Fault.StoreSavesBeforeFailing = 1;

        var runId = await RunAsync(factory);

        using var client = await StaffClient.OpenAsync(factory);
        var run = await ReadRunAsync(client, runId);
        Assert.Equal("failed", run.GetProperty("outcome").GetString());
        Assert.Equal("The records of this run could not be saved.", run.GetProperty("failure_reason").GetString());
        var stages = run.GetProperty("stages").EnumerateArray().ToDictionary(stage => stage.GetProperty("stage").GetString()!);
        Assert.Equal("failed", stages["store"].GetProperty("status").GetString());
        Assert.Equal("The records of this run could not be saved.", stages["store"].GetProperty("reason").GetString());
        Assert.Equal("not_run", stages["publish"].GetProperty("status").GetString());
        // The run stopped at the source whose records could not be saved: 2 of 3 were read.
        Assert.Equal((2, 3), (run.GetProperty("sources_checked").GetInt32(), run.GetProperty("sources_total").GetInt32()));
        Assert.DoesNotContain("10.0.0.12", run.GetRawText(), StringComparison.Ordinal);
        // Exactly what was served before the run began.
        Assert.Equal(before, (await ReadArticleAsync(client, id)).GetRawText());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
        Assert.Equal(1, await db.Articles.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await db.StagedArticles.CountAsync(TestContext.Current.CancellationToken));
        Assert.Null((await db.RunLeases.SingleAsync(TestContext.Current.CancellationToken)).ActiveRunId);
    }

    private static async Task<JsonElement> ReadArticleAsync(HttpClient client, string id) =>
        await client.GetFromJsonAsync<JsonElement>(new Uri($"/admin/v1/articles/{id}", UriKind.Relative), TestContext.Current.CancellationToken);

    private static async Task<string> ArticleIdAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<KeryxDbContext>().Articles.Select(article => article.PublicId).SingleAsync(TestContext.Current.CancellationToken);
    }

    private sealed class Factory : ApiFactory
    {
        public SaveFault.Switch Fault { get; } = new();

        protected override void ConfigureServices(IServiceCollection services)
        {
            services.AddScoped<IAppDbContext>(provider => new SaveFault(provider.GetRequiredService<KeryxDbContext>(), Fault));
        }
    }
}
