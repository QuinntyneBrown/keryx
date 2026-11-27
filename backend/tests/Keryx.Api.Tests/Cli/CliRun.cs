using Keryx.Api.Tests.Fakes;
using Keryx.Cli;
using Keryx.Infrastructure.Persistence.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Keryx.Api.Tests.Cli;

/// <summary>What one run of the CLI left: its exit code and what it wrote.</summary>
public sealed record CliResult(int ExitCode, string Output, string Error);

/// <summary>
/// One operator's environment for the CLI: a store, a clock and the migrations. Each
/// <see cref="RunAsync"/> is a new process's worth of services over the same environment, as a
/// second run of a command is.
/// </summary>
public sealed class CliRun
{
    public string Store { get; } = TestStore.NewName();

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 27, 15, 0, 0, TimeSpan.Zero));

    /// <summary>The migrations a run finds pending. By default there are none.</summary>
    public RecordingMigrator Migrator { get; init; } = new();

    /// <summary>The connection string the environment gives. Empty stands for a setting that is missing.</summary>
    public string ConnectionString { get; init; } = TestStore.UnusedConnection;

    public async Task<CliResult> RunAsync(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await KeryxCli.RunAsync(
            args,
            output,
            error,
            builder =>
            {
                builder.Configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>(StringComparer.Ordinal) { ["ConnectionStrings:Keryx"] = ConnectionString });
                builder.Services.UseTestStore(Store);
                builder.Services.RemoveAll<TimeProvider>();
                builder.Services.AddSingleton<TimeProvider>(Time);
                builder.Services.RemoveAll<IDatabaseMigrator>();
                builder.Services.AddSingleton<IDatabaseMigrator>(Migrator);
            },
            TestContext.Current.CancellationToken);
        return new CliResult(exitCode, output.ToString(), error.ToString());
    }
}
