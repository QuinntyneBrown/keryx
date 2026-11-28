using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Fakes;
using Keryx.Api.Tests.Session;
using Keryx.Application.Catalogue;
using Keryx.Domain.Alerts;
using Keryx.Domain.Catalogue;
using Keryx.Domain.Clients;
using Keryx.Domain.Collection;
using Keryx.Domain.Seeding;
using Keryx.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Keryx.Api.Tests.Catalogue;

// Acceptance Test
// Traces to: L2-079 AC1, AC2, AC3, AC4, AC5; L2-019 AC1; L2-037 AC5
// Description: Given "Keep articles for" a number of months, the retention run deletes every
// article collected before the cutoff (the clock less that many months), with its provenance,
// and keeps the rest; its run is recorded with worker retention and the number deleted as a
// number. The period is read at the start of every run, so a shortened one applies at the next.
// An event goes with its expired article, or, with none, when it started before the cutoff; an
// organization is kept while an article, an event or a source (by its normalized name) refers to
// it, and goes when none does. A finished run started before the cutoff goes with its stages,
// issue occurrences, staged items and alert records, and a closed issue left with no occurrence
// goes with it; a running run, an open issue and an issue that still occurs are kept. API usage
// hours go after 30 days, whatever the period.
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

    [Fact]
    public async Task Given_events_and_organizations_When_retention_runs_Then_those_only_expired_records_refer_to_go()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        var ct = TestContext.Current.CancellationToken;
        var future = new EventTime(factory.Time.GetUtcNow().AddDays(30), "America/Toronto");
        await using (var store = TestStore.Open(factory.Store))
        {
            var byOldArticle = Organization.Named("Riverside Runners", "toronto", null, 1, "Example feed", Cutoff.AddDays(-400));
            var byKeptArticle = Organization.Named("Grace Fitness", "toronto", null, 1, "Example feed", Cutoff.AddDays(-400));
            var bySource = Organization.Named("The Grace Toronto Church", "toronto", null, 1, "Example feed", Cutoff.AddDays(-400));
            var byLaterEvent = Organization.Named("Lakeshore Camp", "toronto", null, 1, "Example feed", Cutoff.AddDays(-400));
            var byNothing = Organization.Named("Old Mission", "toronto", null, 1, "Example feed", Cutoff.AddDays(-400));
            store.Sources.Add(Keryx.Domain.Sources.Source.Add(Keryx.Domain.Sources.SourceKind.Rss, "https://grace.example.org/feed", "grace  TORONTO church", "toronto", Cutoff.AddDays(-400)));
            var old = NewArticle("old", byOldArticle, Cutoff.AddDays(-1));
            var kept = NewArticle("kept", byKeptArticle, Cutoff.AddDays(1));
            var unlinkedEarlier = NewArticle("unlinked-earlier", null, Cutoff.AddDays(1));
            var unlinkedLater = NewArticle("unlinked-later", null, Cutoff.AddDays(1));
            store.Articles.AddRange(old, kept, unlinkedEarlier, unlinkedLater);
            store.Organizations.AddRange(bySource, byNothing);
            store.Events.AddRange(
                Event("with-old", byOldArticle, old, future),
                Event("with-kept", null, kept, future),
                Event("unlinked-earlier", null, unlinkedEarlier, new EventTime(Cutoff.AddTicks(-1), "America/Toronto")),
                Event("unlinked-later", byLaterEvent, unlinkedLater, new EventTime(Cutoff, "America/Toronto")));
            await store.SaveChangesAsync(ct);

            // An event whose article went another way keeps no link.
            store.Articles.RemoveRange(unlinkedEarlier, unlinkedLater);
            await store.SaveChangesAsync(ct);
        }

        var runId = await RetainAsync(factory);

        await using (var store = TestStore.Open(factory.Store))
        {
            Assert.Equal(["kept"], await store.Articles.Select(article => article.Title).ToListAsync(ct));
            Assert.Equal(
                ["with-kept", "unlinked-later"],
                await store.Events.OrderBy(item => item.Id).Select(item => item.Title).ToListAsync(ct));
            Assert.Equal(
                ["Grace Fitness", "Lakeshore Camp", "The Grace Toronto Church"],
                await store.Organizations.OrderBy(organization => organization.Name).Select(organization => organization.Name).ToListAsync(ct));
        }

        using var staff = await StaffClient.OpenAsync(factory);
        // One article, two events and two organizations.
        Assert.Equal(5, await DeletedAsync(staff, runId));
    }

    [Fact]
    public async Task Given_runs_older_than_24_months_When_retention_runs_Then_they_go_with_their_stage_results_and_issues()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        var ct = TestContext.Current.CancellationToken;
        var now = factory.Time.GetUtcNow();
        var old = Finished(Cutoff.AddTicks(-1));
        var running = Run.Start(RunWorker.Collection, RunTrigger.Schedule, Cutoff.AddDays(-1));
        var kept = Finished(Cutoff);
        var closedOld = RunIssue.AlertNotSent(RunWorker.Collection, "closed, old only", Cutoff.AddDays(-2));
        var openOld = RunIssue.AlertNotSent(RunWorker.Collection, "open, old only", Cutoff.AddDays(-2));
        var closedBoth = RunIssue.AlertNotSent(RunWorker.Collection, "closed, old and kept", Cutoff.AddDays(-2));
        closedOld.Close();
        closedBoth.Close();
        var client = ApiClient.Create("Christian Fitness", ["articles:read"], "kx_live_test", "hash", now);
        await using (var store = TestStore.Open(factory.Store))
        {
            store.Runs.AddRange(old, running, kept);
            store.RunIssues.AddRange(closedOld, openOld, closedBoth);
            store.ApiClients.Add(client);
            await store.SaveChangesAsync(ct);
            store.IssueOccurrences.AddRange(
                IssueOccurrence.Of(closedOld, old, old.StartedAt),
                IssueOccurrence.Of(openOld, old, old.StartedAt),
                IssueOccurrence.Of(closedBoth, old, old.StartedAt),
                IssueOccurrence.Of(closedBoth, kept, kept.StartedAt));
            store.AlertRecords.AddRange(AlertRecord.ForRun(old), AlertRecord.ForRun(kept), AlertRecord.ForIssue(closedOld), AlertRecord.ForIssue(openOld));
            store.StagedArticles.AddRange(Staged(old), Staged(kept));
            store.ApiUsageHours.AddRange(
                ApiUsageHour.Start(client.Id, now.AddDays(-30).AddHours(-1)),
                ApiUsageHour.Start(client.Id, now.AddDays(-30)));
            await store.SaveChangesAsync(ct);
        }

        var runId = await RetainAsync(factory);

        await using (var store = TestStore.Open(factory.Store))
        {
            Assert.Equal(
                [running.PublicId, kept.PublicId, runId],
                await store.Runs.OrderBy(run => run.Id).Select(run => run.PublicId).ToListAsync(ct));
            Assert.Equal(6, store.Runs.Where(run => run.PublicId == kept.PublicId).SelectMany(run => run.Stages).Count());
            Assert.Equal(
                ["open, old only", "closed, old and kept"],
                await store.RunIssues.OrderBy(issue => issue.Id).Select(issue => issue.Detail).ToListAsync(ct));
            Assert.Equal([kept.Id], await store.IssueOccurrences.Select(occurrence => occurrence.RunId).ToListAsync(ct));
            Assert.Equal(
                [((long?)kept.Id, (long?)null), (null, openOld.Id)],
                (await store.AlertRecords.OrderBy(record => record.Id).Select(record => new { record.RunId, record.IssueId }).ToListAsync(ct))
                    .Select(record => (record.RunId, record.IssueId)));
            Assert.Equal([kept.Id], await store.StagedArticles.Select(staged => staged.RunId).ToListAsync(ct));
            Assert.Equal([now.AddDays(-30)], await store.ApiUsageHours.Select(hour => hour.HourStart).ToListAsync(ct));
        }

        using var staff = await StaffClient.OpenAsync(factory);
        // One run, one issue and one hour of usage.
        Assert.Equal(3, await DeletedAsync(staff, runId));
    }

    private static Run Finished(DateTimeOffset startedAt)
    {
        var run = Run.Start(RunWorker.Collection, RunTrigger.Schedule, startedAt);
        run.Finish(new RunCounts(), startedAt.AddMinutes(1));
        return run;
    }

    private static StagedArticle Staged(Run run) =>
        StagedArticle.Of(run.Id, StagedAction.Add, null, 1, "Example feed",
            new ArticleFacts($"https://example.org/staged/{run.PublicId}", "Staged", "All welcome.", "events", "toronto", [], null, 0.70m, PublicationStatus.Published));

    private static Article NewArticle(string title, Organization? organization, DateTimeOffset collectedAt) =>
        Article.Collect(
            new ArticleFacts($"https://example.org/{title}", title, "All welcome.", "events", "toronto", [], null, 0.70m, PublicationStatus.Published),
            organization, 1, "Example feed", collectedAt);

    private static EventRecord Event(string title, Organization? organization, Article article, EventTime start) =>
        EventRecord.From(
            new EventFacts($"https://example.org/events/{title}", title, start, null, "Trinity Bellwoods Park", "toronto", 0.70m, PublicationStatus.Published),
            organization, article, 1, "Example feed", article.CollectedAt);

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
