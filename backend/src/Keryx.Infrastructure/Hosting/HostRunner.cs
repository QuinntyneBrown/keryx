using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Keryx.Infrastructure.Hosting;

/// <summary>
/// Runs a Keryx process and turns a missing required setting into a clear stop (L2-096): each
/// message names the setting, never its value, and the process exits with
/// <see cref="MissingSettingExitCode"/>. The API, the worker and the CLI all start through here.
/// </summary>
public static class HostRunner
{
    /// <summary>The exit code of a process that stopped because a required setting is missing.</summary>
    public const int MissingSettingExitCode = 2;

    /// <summary>Builds the host and runs it until it is stopped. Returns the process's exit code.</summary>
    public static async Task<int> RunAsync(Func<IHost> build, TextWriter error, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(error);
        try
        {
            using var host = build();
            await host.RunAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }
        catch (OptionsValidationException exception)
        {
            return await ReportAsync(exception, error).ConfigureAwait(false);
        }
    }

    /// <summary>Writes one line per missing setting and returns <see cref="MissingSettingExitCode"/>.</summary>
    public static async Task<int> ReportAsync(OptionsValidationException exception, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(error);
        foreach (var failure in exception.Failures)
        {
            await error.WriteLineAsync(failure).ConfigureAwait(false);
        }

        return MissingSettingExitCode;
    }
}
