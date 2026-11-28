using Keryx.Api.Tests.Session;
using Keryx.Application.Alerts;
using Keryx.Application.Sources;
using Keryx.Domain.Collection;
using Keryx.Domain.Sources;
using Keryx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using static Keryx.Api.Tests.Runs.CollectionRuns;

namespace Keryx.Api.Tests.Alerts;

// Acceptance Test
// Traces to: L2-077 AC1, AC2, AC3, AC6
// Description: With "Alert when a source fails 3 runs in a row" and an alert address, the third
// consecutive failure of a source sends one plain-text e-mail naming the source, the reason and
// the streak, linking only to the admin app; later failures in that streak send none. A source
// that recovers and fails 3 runs again is a new streak and a new e-mail. A muted issue, or no
// alert address, sends nothing. Each test has its own store, clock and mailbox.
public sealed class SourceFailingAlertTests
{
    // A query string, which no e-mail may carry (L2-020 AC5).
    private const string Gone = "https://gone.example/feed?utm_source=keryx";
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    [Fact]
    public async Task Given_a_threshold_of_3_When_a_source_fails_its_third_and_fourth_runs_Then_one_e_mail_is_sent_after_the_third()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await Alerting.ConfigureAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, Gone, "Gone Gym", "toronto");

        await RunAsync(factory);
        factory.Time.Advance(Interval);
        await RunAsync(factory);
        factory.Time.Advance(Interval);
        Assert.Empty(factory.Mail.Sent);
        var third = await RunAsync(factory);
        factory.Time.Advance(Interval);
        await RunAsync(factory);

        var mail = Assert.Single(factory.Mail.Sent);
        Assert.Equal(new MailMessage(
            Alerting.Address,
            "Keryx: Gone Gym has failed 3 runs in a row",
            "Gone Gym has failed 3 collection runs in a row, since Fri 27 Nov, 10:00 AM.\n" +
            "\n" +
            "Reason: Feed returned 404. Nothing was found at https://gone.example/feed.\n" +
            "\n" +
            $"See the latest run: https://admin.keryx.test/runs?run={third}\n" +
            "\n" +
            "You won't get another e-mail about this until the source recovers and fails again. " +
            "To stop these e-mails for this problem, mute the issue on the run.\n" +
            "\n" +
            "Change alerts in Settings: https://admin.keryx.test/settings\n"), mail);
    }

    [Fact]
    public async Task Given_a_source_name_with_line_breaks_When_its_alert_is_composed_Then_each_break_is_one_space()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await Alerting.ConfigureAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, Gone, "Gone\r\nBcc: x@example.com   Gym", "toronto");

        await FailRunsAsync(factory, 3);

        var mail = Assert.Single(factory.Mail.Sent);
        Assert.Equal("Keryx: Gone Bcc: x@example.com Gym has failed 3 runs in a row", mail.Subject);
        Assert.StartsWith("Gone Bcc: x@example.com Gym has failed 3 collection runs", mail.PlainTextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_a_source_alerted_then_recovered_When_it_fails_3_runs_in_a_row_again_Then_a_new_e_mail_is_sent()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await Alerting.ConfigureAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, Gone, "Gone Gym", "toronto");
        await FailRunsAsync(factory, 3);
        factory.Sources.AnswerFeed(Gone, "<rss version=\"2.0\"><channel><title>Feed</title></channel></rss>");
        await RunAsync(factory);
        factory.Time.Advance(Interval);
        factory.Sources.Answer(Gone, SourceFetchResult.Failed(FetchFailureReason.NotFound));
        await FailRunsAsync(factory, 2);
        Assert.Single(factory.Mail.Sent);

        await FailRunsAsync(factory, 1);

        Assert.Equal(2, factory.Mail.Sent.Count);
        Assert.Contains("since Sat 28 Nov, 10:00 AM.", factory.Mail.Sent.Last().PlainTextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_a_muted_issue_When_its_source_keeps_failing_Then_no_e_mail_is_sent()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await Alerting.ConfigureAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, Gone, "Gone Gym", "toronto");
        await FailRunsAsync(factory, 1);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
            (await db.RunIssues.SingleAsync(TestContext.Current.CancellationToken)).Mute();
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await FailRunsAsync(factory, 4);

        Assert.Empty(factory.Mail.Sent);
        Assert.Equal(0, factory.Mail.Attempts);
    }

    [Fact]
    public async Task Given_no_alert_address_When_a_source_fails_3_runs_in_a_row_Then_no_e_mail_is_sent()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, Gone, "Gone Gym", "toronto");

        await FailRunsAsync(factory, 3);

        Assert.Equal(0, factory.Mail.Attempts);
    }

    [Fact]
    public async Task Given_no_mail_service_is_configured_When_a_source_fails_3_runs_in_a_row_Then_no_e_mail_is_attempted()
    {
        using var factory = new ApiFactory();
        factory.Mail.IsConfigured = false;
        await StaffClient.SeedAsync(factory);
        await Alerting.ConfigureAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, Gone, "Gone Gym", "toronto");

        await FailRunsAsync(factory, 3);

        Assert.Equal(0, factory.Mail.Attempts);
    }

    private static async Task FailRunsAsync(ApiFactory factory, int count)
    {
        for (var i = 0; i < count; i++)
        {
            await RunAsync(factory);
            factory.Time.Advance(Interval);
        }
    }
}
