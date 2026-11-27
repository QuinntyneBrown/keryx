namespace Keryx.Cli;

/// <summary>The CLI's commands, behind one entry a test can call with its own writers.</summary>
public static class KeryxCli
{
    /// <summary>The exit code of a command line that names no known command.</summary>
    public const int UsageExitCode = 1;

    public const string Usage = """
        Usage: Keryx.Cli <command>

          migrate   Apply the pending database migrations. Run again, it changes nothing.
          seed      Add the seed data that is missing. Run again, it changes nothing.

        Settings (environment variables): ConnectionStrings__Keryx.
        See tools/deploy/configuration.md.
        """;

    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        await error.WriteLineAsync(Usage).ConfigureAwait(false);
        return UsageExitCode;
    }
}
