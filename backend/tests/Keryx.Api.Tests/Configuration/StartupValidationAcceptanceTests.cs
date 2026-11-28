// Acceptance Test
// Traces to: L2-096
// Description: A missing required setting stops the API, the worker and the CLI at startup with a message naming the setting and nothing else

using Keryx.Api.Tests.Cli;
using Keryx.Api.Tests.Fakes;
using Keryx.Infrastructure.Hosting;
using Keryx.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Keryx.Api.Tests.Configuration;

public sealed class StartupValidationAcceptanceTests
{
    private const int MissingSetting = 2;

    private const string BaseUrl = "--Keryx:Api:PublicBaseUrl=https://api.keryx.test/v1";

    private static readonly string Message = $"Missing required setting: ConnectionStrings__Keryx{Environment.NewLine}";

    // A process that did start is stopped after this long, so a missing check fails the test instead of hanging it.
    private static readonly TimeSpan StartedAnyway = TimeSpan.FromSeconds(10);

    // L2-096 AC2, the API. A setting that is empty or blank is missing.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Given_no_database_connection_When_the_API_starts_Then_it_stops_with_exit_code_2_naming_the_setting(string connectionString)
    {
        using var error = new StringWriter();
        using var startedAnyway = new CancellationTokenSource(StartedAnyway);

        var exitCode = await HostRunner.RunAsync(
            () => ApiHost.Build(["--urls", "http://127.0.0.1:0", $"--ConnectionStrings:Keryx={connectionString}",
                "--Keryx:Oidc:Authority=https://id.example.test", "--Keryx:Oidc:ClientId=keryx-test",
                "--Keryx:Fetch:UserAgentContactUrl=https://keryx.example/collector", BaseUrl]),
            error,
            startedAnyway.Token);

        Assert.Equal(MissingSetting, exitCode);
        Assert.Equal(Message, error.ToString());
    }

    [Fact]
    public async Task Given_missing_provider_configuration_When_the_API_starts_Then_it_names_both_settings()
    {
        using var error = new StringWriter();
        using var startedAnyway = new CancellationTokenSource(StartedAnyway);
        var exitCode = await HostRunner.RunAsync(
            () => ApiHost.Build(["--urls", "http://127.0.0.1:0", $"--ConnectionStrings:Keryx={TestStore.UnusedConnection}",
                "--Keryx:Oidc:Authority=", "--Keryx:Oidc:ClientId=", "--Keryx:Fetch:UserAgentContactUrl=https://keryx.example/collector", BaseUrl]),
            error, startedAnyway.Token);
        Assert.Equal(MissingSetting, exitCode);
        Assert.Contains("Keryx__Oidc__Authority", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("Keryx__Oidc__ClientId", error.ToString(), StringComparison.Ordinal);
    }

    // L2-096, L2-067: the consumer API's public base address is required: an http(s) address ending in /v1.
    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("ftp://api.keryx.test/v1")]
    [InlineData("https://api.keryx.test/")]
    public async Task Given_no_public_base_url_When_the_API_starts_Then_it_stops_naming_the_setting(string baseUrl)
    {
        using var error = new StringWriter();
        using var startedAnyway = new CancellationTokenSource(StartedAnyway);

        var exitCode = await HostRunner.RunAsync(
            () => ApiHost.Build(["--urls", "http://127.0.0.1:0", $"--ConnectionStrings:Keryx={TestStore.UnusedConnection}",
                "--Keryx:Oidc:Authority=https://id.example.test", "--Keryx:Oidc:ClientId=keryx-test",
                "--Keryx:Fetch:UserAgentContactUrl=https://keryx.example/collector", $"--Keryx:Api:PublicBaseUrl={baseUrl}"]),
            error,
            startedAnyway.Token);

        Assert.Equal(MissingSetting, exitCode);
        Assert.Equal($"Missing required setting: Keryx__Api__PublicBaseUrl{Environment.NewLine}", error.ToString());
    }

    // L2-021 AC3: the contact URL every request names is required, and must be https.
    [Theory]
    [InlineData("")]
    [InlineData("http://keryx.example/collector")]
    [InlineData("not a url")]
    public async Task Given_no_https_contact_url_When_the_API_starts_Then_it_stops_naming_the_setting(string contactUrl)
    {
        using var error = new StringWriter();
        using var startedAnyway = new CancellationTokenSource(StartedAnyway);

        var exitCode = await HostRunner.RunAsync(
            () => ApiHost.Build(["--urls", "http://127.0.0.1:0", $"--ConnectionStrings:Keryx={TestStore.UnusedConnection}",
                "--Keryx:Oidc:Authority=https://id.example.test", "--Keryx:Oidc:ClientId=keryx-test",
                $"--Keryx:Fetch:UserAgentContactUrl={contactUrl}", BaseUrl]),
            error,
            startedAnyway.Token);

        Assert.Equal(MissingSetting, exitCode);
        Assert.Equal($"Missing required setting: Keryx__Fetch__UserAgentContactUrl{Environment.NewLine}", error.ToString());
    }

    // L2-096 AC2, the worker.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Given_no_database_connection_When_the_worker_starts_Then_it_stops_with_exit_code_2_naming_the_setting(string connectionString)
    {
        using var error = new StringWriter();
        using var startedAnyway = new CancellationTokenSource(StartedAnyway);

        var exitCode = await HostRunner.RunAsync(
            () => WorkerHost.Build([], builder => builder.Configuration.AddInMemoryCollection(
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["ConnectionStrings:Keryx"] = connectionString,
                    ["Keryx:Fetch:UserAgentContactUrl"] = "https://keryx.example/collector",
                })),
            error,
            startedAnyway.Token);

        Assert.Equal(MissingSetting, exitCode);
        Assert.Equal(Message, error.ToString());
    }

    // L2-021 AC3: the worker fetches sources too, so it needs the contact URL as the API does.
    [Theory]
    [InlineData("")]
    [InlineData("http://keryx.example/collector")]
    public async Task Given_no_https_contact_url_When_the_worker_starts_Then_it_stops_naming_the_setting(string contactUrl)
    {
        using var error = new StringWriter();
        using var startedAnyway = new CancellationTokenSource(StartedAnyway);

        var exitCode = await HostRunner.RunAsync(
            () => WorkerHost.Build([], builder => builder.Configuration.AddInMemoryCollection(
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["ConnectionStrings:Keryx"] = TestStore.UnusedConnection,
                    ["Keryx:Fetch:UserAgentContactUrl"] = contactUrl,
                    ["OTEL_EXPORTER_OTLP_ENDPOINT"] = string.Empty,
                })),
            error,
            startedAnyway.Token);

        Assert.Equal(MissingSetting, exitCode);
        Assert.Equal($"Missing required setting: Keryx__Fetch__UserAgentContactUrl{Environment.NewLine}", error.ToString());
    }

    // The CLI stops the same way, before its command touches anything.
    [Theory]
    [InlineData("migrate")]
    [InlineData("seed")]
    public async Task Given_no_database_connection_When_a_CLI_command_runs_Then_it_stops_with_exit_code_2_and_changes_nothing(string command)
    {
        var cli = new CliRun { ConnectionString = string.Empty, Migrator = new RecordingMigrator(2) };

        var result = await cli.RunAsync(command);

        Assert.Equal(new CliResult(MissingSetting, string.Empty, Message), result);
        Assert.Equal(0, cli.Migrator.Calls);
        await using var db = TestStore.Open(cli.Store);
        Assert.Equal(0, await db.Regions.CountAsync(TestContext.Current.CancellationToken));
    }
}
