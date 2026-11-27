using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using Keryx.Domain.Collection;
using Keryx.Domain.Sources;
using Keryx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Keryx.Api.Tests.Runs.CollectionRuns;

namespace Keryx.Api.Tests.Runs;

// Acceptance Test
// Traces to: L2-045 AC2, AC3, AC4, AC6 (the data)
// Description: The open issues that need staff attention, most severe first: errors before
// warnings, then the longest streak. Each names its subject and the run it last occurred in;
// muted and closed issues are left out, and the total counts every open one.
public sealed class OpenIssuesTests
{
    private static readonly string[] ItemKeys =
        ["consecutive_runs", "detail", "id", "kind", "run_id", "severity", "since", "subject_id", "subject_name", "subject_type"];

    [Fact]
    public async Task Given_open_muted_and_closed_issues_When_open_issues_are_listed_Then_errors_come_first_then_the_longest_streak()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        var redeemed = await SourceAsync(factory, "https://redeemedrun.ca/feed", "Redeemed Run Club Toronto");
        var lifePoint = await SourceAsync(factory, "https://lifepointfit.ca/feed", "LifePoint Fitness");
        var now = factory.Time.GetUtcNow();
        var earlier = Run.Start(RunWorker.Collection, RunTrigger.Schedule, now.AddHours(-12));
        var latest = Run.Start(RunWorker.Collection, RunTrigger.Schedule, now.AddHours(-6));
        var notFound = RunIssue.ForSource(RunWorker.Collection, IssueSeverity.Error, IssueKind.SourceNotFound, redeemed, "Nothing was found at https://redeemedrun.ca/feed.", now.AddDays(-4));
        for (var i = 0; i < 3; i++)
        {
            notFound.Continue("Nothing was found at https://redeemedrun.ca/feed.");
        }

        var timeout = RunIssue.ForSource(RunWorker.Collection, IssueSeverity.Error, IssueKind.SourceTimeout, lifePoint, "The source did not answer within 30s on 3 attempts.", now.AddHours(-6));
        var held = RunIssue.ForSource(RunWorker.Collection, IssueSeverity.Warning, IssueKind.ItemsHeld, lifePoint, "2 items held for review.", now.AddHours(-12));
        held.Continue("2 items held for review.");
        var skipped = RunIssue.SkippedStart(RunWorker.Collection, now.AddHours(-6));
        var closed = RunIssue.ForSource(RunWorker.Collection, IssueSeverity.Error, IssueKind.SourceBlocked, redeemed, "Blocked.", now.AddDays(-2));
        closed.Close();
        var muted = RunIssue.ForSource(RunWorker.Collection, IssueSeverity.Error, IssueKind.SourceInvalid, lifePoint, "Unreadable.", now.AddDays(-2));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
            db.Runs.AddRange(earlier, latest);
            db.RunIssues.AddRange(notFound, timeout, held, skipped, closed, muted);
            db.IssueOccurrences.AddRange(
                IssueOccurrence.Of(notFound, earlier, now.AddHours(-12)), IssueOccurrence.Of(notFound, latest, now.AddHours(-6)),
                IssueOccurrence.Of(timeout, latest, now.AddHours(-6)), IssueOccurrence.Of(held, earlier, now.AddHours(-12)),
                IssueOccurrence.Of(skipped, latest, now.AddHours(-6)), IssueOccurrence.Of(closed, latest, now.AddHours(-6)),
                IssueOccurrence.Of(muted, latest, now.AddHours(-6)));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            db.Entry(muted).Property(issue => issue.State).CurrentValue = IssueState.Muted;
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var page = await client.GetFromJsonAsync<JsonElement>(new Uri("/admin/v1/issues?state=open&page_size=5", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(4, page.GetProperty("total").GetInt32());
        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal([notFound.PublicId, timeout.PublicId, held.PublicId, skipped.PublicId], items.Select(item => item.GetProperty("id").GetString()));
        var first = items[0];
        Assert.Equal(ItemKeys, first.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(("error", "source_not_found", "source", "Redeemed Run Club Toronto"),
            (first.GetProperty("severity").GetString(), first.GetProperty("kind").GetString(), first.GetProperty("subject_type").GetString(), first.GetProperty("subject_name").GetString()));
        Assert.Equal((4, now.AddDays(-4), latest.PublicId),
            (first.GetProperty("consecutive_runs").GetInt32(), first.GetProperty("since").GetDateTimeOffset(), first.GetProperty("run_id").GetString()));
        Assert.Equal(earlier.PublicId, items[2].GetProperty("run_id").GetString());
        Assert.Equal(("worker", "collection", JsonValueKind.Null),
            (items[3].GetProperty("subject_type").GetString(), items[3].GetProperty("subject_id").GetString(), items[3].GetProperty("subject_name").ValueKind));
    }

    [Fact]
    public async Task Given_7_open_warnings_of_one_run_each_When_5_are_listed_Then_the_5_oldest_come_and_the_total_is_7()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        var now = factory.Time.GetUtcNow();
        var run = Run.Start(RunWorker.Collection, RunTrigger.Schedule, now.AddHours(-1));
        var issues = new List<RunIssue>();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
            db.Runs.Add(run);
            foreach (var i in Enumerable.Range(0, 7))
            {
                var issue = RunIssue.SkippedStart(RunWorker.Collection, now.AddMinutes(-i));
                issues.Add(issue);
                db.RunIssues.Add(issue);
                db.IssueOccurrences.Add(IssueOccurrence.Of(issue, run, now));
            }

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var page = await client.GetFromJsonAsync<JsonElement>(new Uri("/admin/v1/issues?page_size=5", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(7, page.GetProperty("total").GetInt32());
        Assert.Equal(
            issues.OrderBy(issue => issue.Since).Take(5).Select(issue => issue.PublicId),
            page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetString()));
    }

    [Theory]
    [InlineData("state=closed", "invalid_filter")]
    [InlineData("page_size=0", "invalid_page")]
    [InlineData("page_size=51", "invalid_page")]
    public async Task Given_a_value_it_cannot_use_When_issues_are_listed_Then_400_with_its_code(string query, string code)
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);

        using var response = await client.GetAsync(new Uri($"/admin/v1/issues?{query}", UriKind.Relative), TestContext.Current.CancellationToken);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal((HttpStatusCode.BadRequest, code), (response.StatusCode, problem.GetProperty("code").GetString()));
    }

    private static async Task<long> SourceAsync(ApiFactory factory, string address, string name)
    {
        var id = await AddSourceAsync(factory, SourceKind.Rss, address, name, "toronto");
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<KeryxDbContext>().Sources
            .Where(source => source.PublicId == id).Select(source => source.Id).SingleAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<HttpClient> OpenAsync(ApiFactory factory)
    {
        await StaffClient.SeedAsync(factory);
        return await StaffClient.OpenAsync(factory);
    }
}
