// Acceptance Test
// Traces to: L2-095
// Description: Liveness never touches the database, readiness is the schema's state and nothing more, and the API never migrates

using System.Net;
using Keryx.Api.Tests.Fakes;
using Keryx.Infrastructure.Persistence.Schema;
using Microsoft.Extensions.Logging;

namespace Keryx.Api.Tests.Health;

public sealed class HealthAcceptanceTests
{
    private const string SchemaStatusLogger = "Keryx.Api.Health.SchemaStatusLogger";

    private static readonly Uri Live = new("/health/live", UriKind.Relative);
    private static readonly Uri Ready = new("/health/ready", UriKind.Relative);

    // L2-095 AC1: liveness answers whatever the database's state, and asks it nothing.
    [Fact]
    public async Task Given_a_database_that_cannot_be_reached_When_live_is_called_Then_it_answers_200_without_touching_the_database()
    {
        await using var factory = new SchemaApiFactory(FakeSchemaVersion.Unreachable);
        using var client = factory.CreateClient();
        // The API asks once as it starts, to tell the operator; the request must not ask again.
        await factory.Schema.WhenCheckedAsync(1, TestContext.Current.CancellationToken);

        using var response = await client.GetAsync(Live, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, factory.Schema.Checks);
    }

    // L2-095 AC1: one row per state of the database. Each answer is the status word and nothing
    // else: no version, host or error message. No request carries a credential.
    [Theory]
    [InlineData("at the expected version", HttpStatusCode.OK, "Healthy")]
    [InlineData("behind the build", HttpStatusCode.ServiceUnavailable, "Unhealthy")]
    [InlineData("never migrated", HttpStatusCode.ServiceUnavailable, "Unhealthy")]
    [InlineData("ahead of the build", HttpStatusCode.ServiceUnavailable, "Unhealthy")]
    [InlineData("unreachable", HttpStatusCode.ServiceUnavailable, "Unhealthy")]
    public async Task Given_a_database_in_a_state_When_ready_is_called_Then_it_answers_the_status_and_nothing_more(
        string database, HttpStatusCode status, string body)
    {
        await using var factory = new SchemaApiFactory(StateOf(database));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Ready, TestContext.Current.CancellationToken);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(body, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    // The tests' own store (EF Core InMemory) has no migrations: it is always at the expected version.
    [Fact]
    public async Task Given_the_store_the_tests_use_When_ready_is_called_Then_it_answers_200()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Ready, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    // L2-095 AC2.
    [Theory]
    [InlineData("behind the build")]
    [InlineData("never migrated")]
    public async Task Given_a_database_behind_the_build_When_the_API_starts_Then_it_does_not_migrate_reports_not_ready_and_logs_that_migrate_must_be_run(
        string database)
    {
        await using var factory = new SchemaApiFactory(StateOf(database));
        using var client = factory.CreateClient();

        var warning = await factory.Logs.WaitForAsync(
            line => line.Category == SchemaStatusLogger && line.Level == LogLevel.Warning,
            TestContext.Current.CancellationToken);
        using var response = await client.GetAsync(Ready, TestContext.Current.CancellationToken);

        Assert.Contains("migrate", warning.Message, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(0, factory.Migrator.Calls);
        Assert.Single(factory.Logs.Of(SchemaStatusLogger));
    }

    // Telling an operator to run `migrate` would not help either of these.
    [Theory]
    [InlineData("ahead of the build", "newer build")]
    [InlineData("unreachable", "cannot be reached")]
    public async Task Given_a_database_migrate_cannot_help_When_the_API_starts_Then_the_log_says_why_it_is_not_ready(
        string database, string reason)
    {
        await using var factory = new SchemaApiFactory(StateOf(database));
        using var client = factory.CreateClient();

        var warning = await factory.Logs.WaitForAsync(
            line => line.Category == SchemaStatusLogger && line.Level == LogLevel.Warning,
            TestContext.Current.CancellationToken);

        Assert.Contains(reason, warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("migrate", warning.Message, StringComparison.Ordinal);
        Assert.Equal(0, factory.Migrator.Calls);
    }

    [Fact]
    public async Task Given_a_database_at_the_expected_version_When_the_API_starts_Then_it_warns_of_nothing_and_never_migrates()
    {
        await using var factory = new SchemaApiFactory(FakeSchemaVersion.AtExpected);
        using var client = factory.CreateClient();
        await factory.Schema.WhenCheckedAsync(1, TestContext.Current.CancellationToken);

        using var response = await client.GetAsync(Ready, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(factory.Logs.Of(SchemaStatusLogger, LogLevel.Warning));
        Assert.Equal(0, factory.Migrator.Calls);
    }

    private static SchemaState StateOf(string database) => database switch
    {
        "at the expected version" => FakeSchemaVersion.AtExpected,
        "behind the build" => FakeSchemaVersion.Behind,
        "never migrated" => FakeSchemaVersion.NeverMigrated,
        "ahead of the build" => FakeSchemaVersion.Ahead,
        "unreachable" => FakeSchemaVersion.Unreachable,
        _ => throw new ArgumentOutOfRangeException(nameof(database), database, "Unknown state."),
    };
}
