using System.Text.Json;
using Keryx.Api.Tests.Fakes;
using Keryx.Api.Tests.Session;
using Keryx.Application.Abstractions;
using Keryx.Application.Sources;
using Keryx.Domain.Sources;
using Keryx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using static Keryx.Api.Tests.Runs.CollectionRuns;

namespace Keryx.Api.Tests.Alerts;

// Acceptance Test
// Traces to: L2-077 AC5; L2-020
// Description: When the mail service fails, the run carries an alert_not_sent warning and keeps
// its own outcome; the alert is tried once more on the next run, and not again after that. Each
// test has its own store, clock and mailbox.
public sealed class MailFailureTests
{
    private const string Gone = "https://gone.example/feed";
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    [Fact]
    public async Task Given_the_mail_service_fails_When_a_source_alert_is_sent_Then_the_run_has_a_warning_keeps_its_outcome_and_the_next_run_retries_it()
    {
        using var factory = new ApiFactory();
        await ArrangeAsync(factory);
        await FailRunsAsync(factory, 2);
        factory.Mail.Failing = true;

        var third = await FailRunsAsync(factory, 1);

        using var client = await StaffClient.OpenAsync(factory);
        var run = await ReadRunAsync(client, third);
        Assert.Equal("completed_with_issues", run.GetProperty("outcome").GetString());
        var warning = Assert.Single(run.GetProperty("issues").EnumerateArray(), issue => issue.GetProperty("kind").GetString() == "alert_not_sent");
        Assert.Equal(("warning", "worker", "collection"), (
            warning.GetProperty("severity").GetString(),
            warning.GetProperty("subject_type").GetString(),
            warning.GetProperty("subject_id").GetString()));
        Assert.Equal("An alert e-mail could not be sent. Keryx tries it once more on the next run.", warning.GetProperty("detail").GetString());
        Assert.Equal(1, factory.Mail.Attempts);
        Assert.Empty(factory.Mail.Sent);

        factory.Mail.Failing = false;
        var fourth = await FailRunsAsync(factory, 1);

        var retried = Assert.Single(factory.Mail.Sent);
        Assert.Equal("Keryx: Gone Gym has failed 4 runs in a row", retried.Subject);
        Assert.Contains($"See the latest run: https://admin.keryx.test/runs?run={fourth}", retried.PlainTextBody, StringComparison.Ordinal);
        using (var later = await StaffClient.OpenAsync(factory))
        {
            Assert.DoesNotContain((await ReadRunAsync(later, fourth)).GetProperty("issues").EnumerateArray(), IsAlertNotSent);
        }

        await FailRunsAsync(factory, 1);
        Assert.Equal(2, factory.Mail.Attempts);
        Assert.Equal(["closed"], await AlertNotSentStatesAsync(factory));
    }

    [Fact]
    public async Task Given_the_retry_fails_too_When_later_runs_finish_Then_the_alert_is_not_tried_again()
    {
        using var factory = new ApiFactory();
        await ArrangeAsync(factory);
        await FailRunsAsync(factory, 2);
        factory.Mail.Failing = true;
        await FailRunsAsync(factory, 1);

        var fourth = await FailRunsAsync(factory, 1);
        factory.Mail.Failing = false;
        await FailRunsAsync(factory, 2);

        Assert.Equal(2, factory.Mail.Attempts);
        Assert.Empty(factory.Mail.Sent);
        using var client = await StaffClient.OpenAsync(factory);
        var warning = Assert.Single((await ReadRunAsync(client, fourth)).GetProperty("issues").EnumerateArray(), IsAlertNotSent);
        // One issue for the failures in a row, as for any other problem (L2-020 AC1); with nothing
        // sent since, it is still open.
        Assert.Equal(2, warning.GetProperty("consecutive_runs").GetInt32());
        Assert.Equal(["open"], await AlertNotSentStatesAsync(factory));
    }

    [Fact]
    public async Task Given_an_alert_that_was_not_sent_When_its_issue_is_muted_before_the_next_run_Then_it_is_not_retried()
    {
        using var factory = new ApiFactory();
        await ArrangeAsync(factory);
        await FailRunsAsync(factory, 2);
        factory.Mail.Failing = true;
        await FailRunsAsync(factory, 1);
        factory.Mail.Failing = false;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
            (await db.RunIssues.SingleAsync(issue => issue.SourceId != null, TestContext.Current.CancellationToken)).Mute();
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await FailRunsAsync(factory, 1);

        Assert.Equal(1, factory.Mail.Attempts);
    }

    [Fact]
    public async Task Given_a_failed_run_alert_was_not_sent_When_the_next_run_finishes_Then_it_is_sent_naming_the_failed_run()
    {
        using var factory = new StoreFaultFactory();
        await ArrangeAsync(factory);
        factory.Sources.AnswerFeed(Gone, Feed(("Toronto church running club starts spring 5K training", "https://gone.example/news/5k", "Weekly runs.")));
        factory.Fault.StoreArmed = true;
        factory.Mail.Failing = true;
        var failed = await RunAsync(factory);
        factory.Time.Advance(Interval);
        factory.Fault.StoreArmed = false;
        factory.Mail.Failing = false;

        await RunAsync(factory);

        var mail = Assert.Single(factory.Mail.Sent);
        Assert.Equal("Keryx: a collection run failed", mail.Subject);
        Assert.Contains($"See the run: https://admin.keryx.test/runs?run={failed}", mail.PlainTextBody, StringComparison.Ordinal);
        using var client = await StaffClient.OpenAsync(factory);
        var run = await ReadRunAsync(client, failed);
        Assert.Equal("failed", run.GetProperty("outcome").GetString());
        Assert.Single(run.GetProperty("issues").EnumerateArray(), IsAlertNotSent);
    }

    [Fact]
    public async Task Given_a_run_between_two_refused_sends_sent_nothing_When_a_send_is_refused_again_Then_it_is_a_new_issue()
    {
        using var factory = new StoreFaultFactory();
        await ArrangeAsync(factory);
        factory.Sources.AnswerFeed(Gone, Feed(("Toronto church running club starts spring 5K training", "https://gone.example/news/5k", "Weekly runs.")));
        factory.Mail.Failing = true;
        factory.Fault.StoreArmed = true;
        await RunAsync(factory);
        factory.Time.Advance(Interval);
        factory.Fault.StoreArmed = false;
        // The retry is refused too, then a run with nothing to send.
        await RunAsync(factory);
        factory.Time.Advance(Interval);
        await RunAsync(factory);
        factory.Time.Advance(Interval);
        factory.Fault.StoreArmed = true;

        var last = await RunAsync(factory);

        Assert.Equal(["closed", "open"], await AlertNotSentStatesAsync(factory));
        using var client = await StaffClient.OpenAsync(factory);
        var warning = Assert.Single((await ReadRunAsync(client, last)).GetProperty("issues").EnumerateArray(), IsAlertNotSent);
        Assert.Equal(1, warning.GetProperty("consecutive_runs").GetInt32());
    }

    private sealed class StoreFaultFactory : ApiFactory
    {
        public SaveFault.Switch Fault { get; } = new();

        protected override void ConfigureServices(IServiceCollection services) =>
            services.AddScoped<IAppDbContext>(provider => new SaveFault(provider.GetRequiredService<KeryxDbContext>(), Fault));
    }

    private static bool IsAlertNotSent(JsonElement issue) => issue.GetProperty("kind").GetString() == "alert_not_sent";

    private static async Task ArrangeAsync(ApiFactory factory)
    {
        await StaffClient.SeedAsync(factory);
        await Alerting.ConfigureAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, Gone, "Gone Gym", "toronto");
        factory.Sources.Answer(Gone, SourceFetchResult.Failed(FetchFailureReason.NotFound));
    }

    /// <summary>Runs the given number of runs, each failing for the source; returns the last run's id.</summary>
    private static async Task<string> FailRunsAsync(ApiFactory factory, int count)
    {
        var last = string.Empty;
        for (var i = 0; i < count; i++)
        {
            last = await RunAsync(factory);
            factory.Time.Advance(Interval);
        }

        return last;
    }

    private static async Task<List<string>> AlertNotSentStatesAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
        var issues = await db.RunIssues.AsNoTracking().Where(issue => issue.SourceId == null).OrderBy(issue => issue.Id).ToListAsync(TestContext.Current.CancellationToken);
        return [.. issues.Select(issue => issue.State.ToString().ToLowerInvariant())];
    }
}
