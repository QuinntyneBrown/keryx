using Keryx.Api.Tests.Fakes;
using Keryx.Api.Tests.Session;
using Keryx.Application.Abstractions;
using Keryx.Application.Alerts;
using Keryx.Application.Sources;
using Keryx.Domain.Sources;
using Keryx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using static Keryx.Api.Tests.Runs.CollectionRuns;

namespace Keryx.Api.Tests.Alerts;

// Acceptance Test
// Traces to: L2-077 AC4, AC6
// Description: With "When a whole run fails" on, a collection run whose outcome is failed sends
// one plain-text e-mail naming the run, the stage that failed and a generic reason, with its
// reference when it has one; with the switch off, none is sent. Each test has its own store.
public sealed class RunFailedAlertTests
{
    private const string Address = "https://redeemedrun.ca/feed";

    [Fact]
    public async Task Given_the_switch_is_on_When_a_run_fails_at_its_store_stage_Then_one_e_mail_names_the_run_the_stage_and_the_reason()
    {
        using var factory = new StoreFaultFactory();
        await ArrangeAsync(factory);
        factory.Fault.StoreArmed = true;

        var runId = await RunAsync(factory);

        Assert.Equal(new MailMessage(
            Alerting.Address,
            "Keryx: a collection run failed",
            "The collection run that started Fri 27 Nov, 10:00 AM failed at the store stage.\n" +
            "\n" +
            "Reason: The records of this run could not be saved.\n" +
            "\n" +
            $"See the run: https://admin.keryx.test/runs?run={runId}\n" +
            "\n" +
            "Change alerts in Settings: https://admin.keryx.test/settings\n"), Assert.Single(factory.Mail.Sent));
    }

    [Fact]
    public async Task Given_the_switch_is_on_When_a_run_throws_Then_one_e_mail_gives_the_generic_reason_and_its_reference()
    {
        using var factory = new BrokenParserFactory();
        await ArrangeAsync(factory);

        var runId = await RunAsync(factory);

        string correlation;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
            correlation = (await db.Runs.SingleAsync(run => run.PublicId == runId, TestContext.Current.CancellationToken)).CorrelationId!;
        }

        Assert.Equal(new MailMessage(
            Alerting.Address,
            "Keryx: a collection run failed",
            "The collection run that started Fri 27 Nov, 10:00 AM failed before it finished.\n" +
            "\n" +
            "Reason: The run stopped unexpectedly.\n" +
            $"Reference: {correlation}\n" +
            "\n" +
            $"See the run: https://admin.keryx.test/runs?run={runId}\n" +
            "\n" +
            "Change alerts in Settings: https://admin.keryx.test/settings\n"), Assert.Single(factory.Mail.Sent));
        // The exception's own message, with its internal address, is never sent.
        Assert.DoesNotContain("10.0.0.7", factory.Mail.Sent.Single().PlainTextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_the_switch_is_off_When_a_run_fails_Then_no_e_mail_is_sent()
    {
        using var factory = new BrokenParserFactory();
        await ArrangeAsync(factory, alertOnRunFailure: false);

        await RunAsync(factory);

        Assert.Equal(0, factory.Mail.Attempts);
    }

    [Fact]
    public async Task Given_the_switch_is_on_When_a_run_succeeds_Then_no_e_mail_is_sent()
    {
        using var factory = new ApiFactory();
        await ArrangeAsync(factory);

        await RunAsync(factory);

        Assert.Equal(0, factory.Mail.Attempts);
    }

    private static async Task ArrangeAsync(ApiFactory factory, bool alertOnRunFailure = true)
    {
        await StaffClient.SeedAsync(factory);
        await Alerting.ConfigureAsync(factory, values => values with { AlertOnRunFailure = alertOnRunFailure });
        await AddSourceAsync(factory, SourceKind.Rss, Address, "Redeemed Run Club", "toronto");
        factory.Sources.AnswerFeed(Address, Feed(("Toronto church running club starts spring 5K training", "https://redeemedrun.ca/news/5k", "Weekly runs.")));
    }

    private sealed class StoreFaultFactory : ApiFactory
    {
        public SaveFault.Switch Fault { get; } = new();

        protected override void ConfigureServices(IServiceCollection services) =>
            services.AddScoped<IAppDbContext>(provider => new SaveFault(provider.GetRequiredService<KeryxDbContext>(), Fault));
    }

    private sealed class BrokenParserFactory : ApiFactory
    {
        protected override void ConfigureServices(IServiceCollection services)
        {
            services.RemoveAll<IFeedParser>();
            services.AddSingleton<IFeedParser, BrokenParser>();
        }
    }

    private sealed class BrokenParser : IFeedParser
    {
        public ParsedFeed? Parse(SourceKind kind, string content) => throw new InvalidOperationException("The parser broke at 10.0.0.7.");
    }
}
