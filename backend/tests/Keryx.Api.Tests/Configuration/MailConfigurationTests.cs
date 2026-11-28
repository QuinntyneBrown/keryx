// Acceptance Test
// Traces to: L2-077 AC6; L2-096
// Description: The worker sends alerts through the mail service its settings name. With a mail
// host, a sender address and the admin app's origin are required, and a worker missing either
// stops at startup naming it. With no mail host, the worker sends no e-mail and says so once.

using Keryx.Api.Tests.Fakes;
using Keryx.Api.Tests.Workers;
using Keryx.Application.Alerts;
using Keryx.Infrastructure.Hosting;
using Keryx.Infrastructure.Persistence.Schema;
using Keryx.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Keryx.Api.Tests.Configuration;

public sealed class MailConfigurationTests
{
    private static readonly TimeSpan StartedAnyway = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData("Keryx:Mail:From", "Keryx__Mail__From")]
    [InlineData("Keryx:AdminApp:Origin", "Keryx__AdminApp__Origin")]
    public async Task Given_a_mail_host_When_the_worker_starts_without_a_setting_mail_needs_Then_it_stops_naming_it(string missing, string named)
    {
        using var error = new StringWriter();
        using var startedAnyway = new CancellationTokenSource(StartedAnyway);
        var settings = WorkerSettings(withMail: true);
        settings[missing] = string.Empty;

        var exitCode = await HostRunner.RunAsync(
            () => WorkerHost.Build([], builder => builder.Configuration.AddInMemoryCollection(settings)),
            error,
            startedAnyway.Token);

        Assert.Equal(2, exitCode);
        Assert.Equal($"Missing required setting: {named}{Environment.NewLine}", error.ToString());
    }

    [Fact]
    public void Given_a_mail_host_a_sender_and_an_origin_When_the_worker_is_built_Then_alerts_go_through_the_mail_service()
    {
        using var host = WorkerHost.Build([], builder => builder.Configuration.AddInMemoryCollection(WorkerSettings(withMail: true)));

        var mail = host.Services.GetRequiredService<IMailSender>();

        Assert.True(mail.IsConfigured);
        Assert.Equal("SmtpMailSender", mail.GetType().Name);
    }

    [Fact]
    public async Task Given_no_mail_host_When_the_worker_starts_Then_it_sends_no_e_mail_and_says_so_once()
    {
        await using var worker = new WorkerHarness(FakeSchemaVersion.AtExpected);

        await worker.StartAsync();

        var notice = await worker.Logs.WaitForAsync(line => line.Message.Contains("mail", StringComparison.OrdinalIgnoreCase), TestContext.Current.CancellationToken);
        Assert.Equal((LogLevel.Warning, "No mail service is configured (Keryx__Mail__Host): alert e-mails and the daily digest are not sent."), (notice.Level, notice.Message));
        Assert.False(worker.Services.GetRequiredService<IMailSender>().IsConfigured);
    }

    private static Dictionary<string, string?> WorkerSettings(bool withMail)
    {
        var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["ConnectionStrings:Keryx"] = TestStore.UnusedConnection,
            ["Keryx:Fetch:UserAgentContactUrl"] = "https://keryx.example/about-our-collector",
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = string.Empty,
        };
        if (withMail)
        {
            settings["Keryx:Mail:Host"] = "smtp.example.test";
            settings["Keryx:Mail:From"] = "keryx@example.test";
            settings["Keryx:AdminApp:Origin"] = "https://admin.keryx.test";
        }

        return settings;
    }
}
