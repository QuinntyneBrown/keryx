using System.Globalization;
using Keryx.Application;
using Keryx.Application.Seeding;
using Keryx.Infrastructure;
using Keryx.Infrastructure.Hosting;
using Keryx.Infrastructure.Persistence.Schema;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Keryx.Cli;

/// <summary>The CLI's commands, behind one entry a test can call with its own writers.</summary>
public static class KeryxCli
{
    /// <summary>
    /// The exit code of a command line that names no known command. A missing required setting
    /// exits with <see cref="HostRunner.MissingSettingExitCode"/>.
    /// </summary>
    public const int UsageExitCode = 1;

    public const string Usage = """
        Usage: Keryx.Cli <command>

          migrate   Apply the pending database migrations. Run again, it changes nothing.
          seed      Add the seed data that is missing. Run again, it changes nothing.

        Settings (environment variables): ConnectionStrings__Keryx.
        See tools/deploy/configuration.md.
        """;

    private const string Migrate = "migrate";
    private const string Seed = "seed";

    /// <summary>
    /// Runs one command and returns the process's exit code. <paramref name="configure"/> lets a
    /// test put fakes behind the contracts; the process passes none.
    /// </summary>
    public static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        Action<HostApplicationBuilder>? configure = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        if (args is not [Migrate or Seed])
        {
            await error.WriteLineAsync(Usage).ConfigureAwait(false);
            return UsageExitCode;
        }

        return await HostRunner.GuardAsync(
            async () =>
            {
                using var host = Build(configure);
                // The host is never started, so the settings are checked here as a start would.
                host.Services.GetService<IStartupValidator>()?.Validate();
                var scope = host.Services.CreateAsyncScope();
                await using (scope.ConfigureAwait(false))
                {
                    var line = args[0] == Migrate
                        ? await MigrateAsync(scope.ServiceProvider, cancellationToken).ConfigureAwait(false)
                        : await SeedAsync(scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
                    await output.WriteLineAsync(line).ConfigureAwait(false);
                }

                return 0;
            },
            error).ConfigureAwait(false);
    }

    /// <summary>
    /// The same services the API and the worker use, never started as a host: a command runs
    /// and the process ends. Only warnings reach the console, so a command's own line is its report.
    /// </summary>
    private static IHost Build(Action<HostApplicationBuilder>? configure)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);
        configure?.Invoke(builder);
        return builder.Build();
    }

    private static async Task<string> MigrateAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var applied = await services.GetRequiredService<IDatabaseMigrator>().MigrateAsync(cancellationToken).ConfigureAwait(false);
        return string.Create(CultureInfo.InvariantCulture, $"Migrations applied: {applied}");
    }

    private static async Task<string> SeedAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var added = await services.GetRequiredService<ISender>().Send(new SeedDataCommand(), cancellationToken).ConfigureAwait(false);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Seed data added: regions {added.Regions}, categories {added.Categories}, settings {added.Settings}, sources {added.Sources}");
    }
}
