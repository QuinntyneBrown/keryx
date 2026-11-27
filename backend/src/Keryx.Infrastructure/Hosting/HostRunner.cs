using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Keryx.Infrastructure.Hosting;

/// <summary>
/// Runs a Keryx process and turns a missing required setting into a clear stop (L2-096): one
/// line per setting, naming it and never its value, and exit code
/// <see cref="MissingSettingExitCode"/>. The API, the worker and the CLI all start through here.
/// </summary>
public static class HostRunner
{
    /// <summary>The exit code of a process that stopped because a required setting is missing.</summary>
    public const int MissingSettingExitCode = 2;

    /// <summary>Builds the host and runs it until it is stopped. Returns the process's exit code.</summary>
    public static Task<int> RunAsync(Func<IHost> build, TextWriter error, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(build);
        return GuardAsync(
            async () =>
            {
                using var host = build();
                await host.RunAsync(cancellationToken).ConfigureAwait(false);
                return 0;
            },
            error);
    }

    /// <summary>
    /// Runs <paramref name="run"/> and returns its exit code. When it fails because a required
    /// setting is missing, writes each failure to <paramref name="error"/> and returns
    /// <see cref="MissingSettingExitCode"/>. Any other failure is the caller's.
    /// </summary>
    public static async Task<int> GuardAsync(Func<Task<int>> run, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(error);
        try
        {
            return await run().ConfigureAwait(false);
        }
        catch (Exception exception) when (MissingSettings(exception) is { Count: > 0 } missing)
        {
            foreach (var failure in missing)
            {
                await error.WriteLineAsync(failure).ConfigureAwait(false);
            }

            return MissingSettingExitCode;
        }
    }

    /// <summary>
    /// The failures of options validation, or none when the exception is something else. One
    /// options class that fails throws its own exception; several throw them together.
    /// </summary>
    private static List<string> MissingSettings(Exception exception) => exception switch
    {
        OptionsValidationException invalid => [.. invalid.Failures],
        AggregateException several when several.InnerExceptions.All(inner => inner is OptionsValidationException) =>
            [.. several.InnerExceptions.Cast<OptionsValidationException>().SelectMany(invalid => invalid.Failures)],
        _ => [],
    };
}
