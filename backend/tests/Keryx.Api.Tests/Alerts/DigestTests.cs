using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using Keryx.Application.Alerts;
using Keryx.Domain.Collection;
using Keryx.Domain.Sources;
using Keryx.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using static Keryx.Api.Tests.Runs.CollectionRuns;

namespace Keryx.Api.Tests.Alerts;

// Acceptance Test
// Traces to: L2-078 AC1, AC2, AC3, AC4; L2-077 AC5, AC6
// Description: The digest worker records a run with worker alert_digest every time. With
// "Daily digest of held articles" on, an alert address and articles in Needs review, it sends one
// plain-text e-mail with the count, up to 20 titles newest first, and a link to Articles filtered
// to Needs review; otherwise it sends nothing. A refused send is a warning on its run. Each test
// has its own store, clock and mailbox.
public sealed class DigestTests
{
    private const string Global = "https://worldfaithfit.org/feed";

    [Fact]
    public async Task Given_6_articles_need_review_When_the_digest_runs_Then_one_e_mail_lists_the_count_the_titles_and_the_link()
    {
        using var factory = new ApiFactory();
        await ArrangeAsync(factory, held: 6);

        var runId = await DigestAsync(factory);

        var titles = string.Concat(Enumerable.Range(1, 6).Reverse().Select(n => $"- {Title(n)}\n"));
        Assert.Equal(new MailMessage(
            Alerting.Address,
            "Keryx: 6 articles need review",
            "6 articles are waiting in Needs review.\n" +
            "\n" +
            titles +
            "\n" +
            "Review them: https://admin.keryx.test/articles?status=needs_review\n" +
            "\n" +
            "Change alerts in Settings: https://admin.keryx.test/settings\n"), Assert.Single(factory.Mail.Sent));
        var run = await ReadAsync(factory, runId);
        Assert.Equal(("alert_digest", "succeeded"), (run.GetProperty("worker").GetString(), run.GetProperty("outcome").GetString()));
        Assert.Equal((0, 0, 0), (run.GetProperty("added").GetInt32(), run.GetProperty("updated").GetInt32(), run.GetProperty("unchanged").GetInt32()));
    }

    [Fact]
    public async Task Given_22_articles_need_review_When_the_digest_runs_Then_it_lists_the_newest_20_and_says_how_many_more()
    {
        using var factory = new ApiFactory();
        await ArrangeAsync(factory, held: 22);

        await DigestAsync(factory);

        var body = Assert.Single(factory.Mail.Sent).PlainTextBody;
        Assert.StartsWith("22 articles are waiting in Needs review.\n\n- Running and faith retreat 22\n", body, StringComparison.Ordinal);
        Assert.Contains("- Running and faith retreat 3\n…and 2 more.\n\nReview them:", body, StringComparison.Ordinal);
        Assert.Equal(20, body.Split('\n').Count(line => line.StartsWith("- ", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Given_one_article_needs_review_When_the_digest_runs_Then_it_is_worded_for_one()
    {
        using var factory = new ApiFactory();
        await ArrangeAsync(factory, held: 1);

        await DigestAsync(factory);

        var mail = Assert.Single(factory.Mail.Sent);
        Assert.Equal("Keryx: 1 article needs review", mail.Subject);
        Assert.StartsWith("1 article is waiting in Needs review.\n", mail.PlainTextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_no_article_needs_review_When_the_digest_runs_Then_no_e_mail_is_sent_and_its_run_is_recorded()
    {
        using var factory = new ApiFactory();
        await ArrangeAsync(factory, held: 0);

        var runId = await DigestAsync(factory);

        Assert.Equal(0, factory.Mail.Attempts);
        Assert.Equal("succeeded", (await ReadAsync(factory, runId)).GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task Given_the_switch_is_off_When_the_digest_runs_Then_no_e_mail_is_sent()
    {
        using var factory = new ApiFactory();
        await ArrangeAsync(factory, held: 3, digest: false);

        var runId = await DigestAsync(factory);

        Assert.Equal(0, factory.Mail.Attempts);
        Assert.Equal("alert_digest", (await ReadAsync(factory, runId)).GetProperty("worker").GetString());
    }

    [Fact]
    public async Task Given_the_mail_service_fails_When_the_digest_runs_Then_its_run_completed_with_an_alert_not_sent_warning()
    {
        using var factory = new ApiFactory();
        await ArrangeAsync(factory, held: 2);
        factory.Mail.Failing = true;

        var runId = await DigestAsync(factory);

        var run = await ReadAsync(factory, runId);
        Assert.Equal("completed_with_issues", run.GetProperty("outcome").GetString());
        var issue = Assert.Single(run.GetProperty("issues").EnumerateArray());
        Assert.Equal(("alert_not_sent", "warning", "alert_digest", "The daily digest could not be sent."), (
            issue.GetProperty("kind").GetString(),
            issue.GetProperty("severity").GetString(),
            issue.GetProperty("subject_id").GetString(),
            issue.GetProperty("detail").GetString()));
    }

    [Fact]
    public async Task Given_a_held_title_with_line_breaks_When_the_digest_lists_it_Then_each_break_is_one_space()
    {
        using var factory = new ApiFactory();
        await ArrangeAsync(factory, held: 0);
        await AddSourceAsync(factory, SourceKind.Rss, Global, "World Faith Fit", "global");
        factory.Sources.AnswerFeed(Global, Feed(("Running and faith\n- forged line", "https://worldfaithfit.org/x", "All welcome.")));
        await RunAsync(factory);

        await DigestAsync(factory);

        Assert.Contains("\n- Running and faith - forged line\n", Assert.Single(factory.Mail.Sent).PlainTextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_the_digest_throws_When_it_runs_Then_its_run_is_failed_and_no_failed_run_alert_is_sent()
    {
        using var factory = new NoOriginFactory();
        await ArrangeAsync(factory, held: 2);

        var runId = await DigestAsync(factory);

        var run = await ReadAsync(factory, runId);
        Assert.Equal(("failed", "The run stopped unexpectedly."), (run.GetProperty("outcome").GetString(), run.GetProperty("failure_reason").GetString()));
        // "A collection run failed" is about collection runs only (L2-077 AC4).
        Assert.Equal(0, factory.Mail.Attempts);
    }

    /// <summary>No admin app origin: composing the digest throws, as a misconfigured worker would.</summary>
    private sealed class NoOriginFactory : ApiFactory
    {
        protected override void ConfigureServices(IServiceCollection services) =>
            services.Configure<AlertOptions>(options => options.Origin = null);
    }

    private static string Title(int n) => string.Create(CultureInfo.InvariantCulture, $"Running and faith retreat {n}");

    /// <summary>
    /// Settings with the address and the switch; then <paramref name="held"/> articles held for
    /// review, from a source in the global region: "running" and "faith" score 0.48, below 0.60.
    /// </summary>
    private static async Task ArrangeAsync(ApiFactory factory, int held, bool digest = true)
    {
        await StaffClient.SeedAsync(factory);
        await Alerting.ConfigureAsync(factory, values => values with { DailyDigest = digest });
        if (held == 0)
        {
            return;
        }

        await AddSourceAsync(factory, SourceKind.Rss, Global, "World Faith Fit", "global");
        factory.Sources.AnswerFeed(Global, Feed([.. Enumerable.Range(1, held)
            .Select(n => (Title(n), string.Create(CultureInfo.InvariantCulture, $"https://worldfaithfit.org/{n}"), "All welcome."))]));
        await RunAsync(factory);
        factory.Time.Advance(TimeSpan.FromHours(1));
    }

    /// <summary>Runs the digest worker's request as its schedule would; returns its run's id.</summary>
    private static async Task<string> DigestAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new SendHeldArticlesDigestCommand(), TestContext.Current.CancellationToken);
        var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
        return await db.Runs.Where(run => run.Worker == RunWorker.AlertDigest).OrderByDescending(run => run.Id)
            .Select(run => run.PublicId).FirstAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<JsonElement> ReadAsync(ApiFactory factory, string runId)
    {
        using var client = await StaffClient.OpenAsync(factory);
        return await client.GetFromJsonAsync<JsonElement>(new Uri($"/admin/v1/runs/{runId}", UriKind.Relative), TestContext.Current.CancellationToken);
    }
}
