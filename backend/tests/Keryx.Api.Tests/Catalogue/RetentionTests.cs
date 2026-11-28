using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Fakes;
using Keryx.Api.Tests.Session;
using Keryx.Application.Catalogue;
using Keryx.Domain.Catalogue;
using Keryx.Domain.Collection;
using Keryx.Domain.Seeding;
using Keryx.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Keryx.Api.Tests.Catalogue;

// Acceptance Test
// Traces to: L2-079 AC1, AC4, AC5; L2-019 AC1
// Description: Given "Keep articles for" a number of months, the retention run deletes every
// article collected before the cutoff (the clock less that many months), with its provenance,
// and keeps the rest; its run is recorded with worker retention and the number deleted as a
// number. The period is read at the start of every run, so a shortened one applies at the next.
// The run is sent as the worker sends it; the worker's schedule is proven on its own.
public sealed class RetentionTests
{
    // The fixture's clock, 2026-11-27T15:00Z, less the seeded 24 months.
    private static readonly DateTimeOffset Cutoff = new(2024, 11, 27, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_keep_for_24_months_When_retention_runs_Then_articles_collected_before_the_cutoff_are_deleted_and_counted()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        var expired = await CollectAsync(factory, Cutoff.AddTicks(-1), Cutoff.AddDays(-400));
        var kept = await CollectAsync(factory, Cutoff, Cutoff.AddMonths(6));

        var runId = await RetainAsync(factory);

        using var staff = await StaffClient.OpenAsync(factory);
        Assert.Equal(
            [HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.OK, HttpStatusCode.OK],
            await StatusesAsync(staff, [.. expired, .. kept]));
        var run = await staff.GetFromJsonAsync<JsonElement>(new Uri($"/admin/v1/runs/{runId}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(
            ("retention", "schedule", "succeeded"),
            (run.GetProperty("worker").GetString(), run.GetProperty("trigger").GetString(), run.GetProperty("outcome").GetString()));
        Assert.Equal(JsonValueKind.Number, run.GetProperty("deleted").ValueKind);
        Assert.Equal(2, run.GetProperty("deleted").GetInt32());
        await using var store = TestStore.Open(factory.Store);
        Assert.Equal(2, await store.Articles.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_the_period_is_shortened_to_12_months_When_the_next_run_finishes_Then_articles_older_than_12_months_are_gone()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        var now = factory.Time.GetUtcNow();
        var older = await CollectAsync(factory, now.AddMonths(-18));
        var newer = await CollectAsync(factory, now.AddMonths(-6));
        var first = await RetainAsync(factory);

        await using (var store = TestStore.Open(factory.Store))
        {
            var settings = await store.Settings.SingleAsync(TestContext.Current.CancellationToken);
            settings.Update(SeedData.DefaultValues() with { RetentionMonths = 12 });
            await store.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var second = await RetainAsync(factory);

        using var staff = await StaffClient.OpenAsync(factory);
        Assert.Equal([HttpStatusCode.NotFound, HttpStatusCode.OK], await StatusesAsync(staff, [.. older, .. newer]));
        Assert.Equal((0, 1), (await DeletedAsync(staff, first), await DeletedAsync(staff, second)));
    }

    private static async Task<List<string>> CollectAsync(ApiFactory factory, params DateTimeOffset[] collectedAt)
    {
        await using var store = TestStore.Open(factory.Store);
        var articles = collectedAt.Select((at, i) => Article.Collect(
            new ArticleFacts($"https://example.org/{at.UtcTicks}/{i}", $"Article {i}", "All welcome.", "events", "toronto", [], null, 0.70m, PublicationStatus.Published),
            null, 1, "Example feed", at)).ToList();
        store.Articles.AddRange(articles);
        await store.SaveChangesAsync(TestContext.Current.CancellationToken);
        return [.. articles.Select(article => article.PublicId)];
    }

    private static async Task<string> RetainAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ApplyRetentionCommand(RunTrigger.Schedule), TestContext.Current.CancellationToken);
        var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
        return await db.Runs.Where(run => run.Worker == RunWorker.Retention).OrderByDescending(run => run.Id)
            .Select(run => run.PublicId).FirstAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<List<HttpStatusCode>> StatusesAsync(HttpClient staff, IEnumerable<string> ids)
    {
        var statuses = new List<HttpStatusCode>();
        foreach (var id in ids)
        {
            using var response = await staff.GetAsync(new Uri($"/admin/v1/articles/{id}", UriKind.Relative), TestContext.Current.CancellationToken);
            statuses.Add(response.StatusCode);
        }

        return statuses;
    }

    private static async Task<int> DeletedAsync(HttpClient staff, string runId) =>
        (await staff.GetFromJsonAsync<JsonElement>(new Uri($"/admin/v1/runs/{runId}", UriKind.Relative), TestContext.Current.CancellationToken))
            .GetProperty("deleted").GetInt32();
}
