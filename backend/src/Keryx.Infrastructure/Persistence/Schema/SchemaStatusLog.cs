using Microsoft.Extensions.Logging;

namespace Keryx.Infrastructure.Persistence.Schema;

/// <summary>
/// What a process tells its operator about the schema, in one place so the API and the worker
/// say the same thing. A line names the command to run and never a version, a host or an error.
/// </summary>
public static partial class SchemaStatusLog
{
    /// <summary>Logs where the schema stands: a warning with what to do, or that it is ready.</summary>
    public static void Report(ILogger logger, SchemaStatus status)
    {
        ArgumentNullException.ThrowIfNull(logger);
        switch (status)
        {
            case SchemaStatus.Ready:
                Ready(logger);
                break;
            case SchemaStatus.Behind:
                Behind(logger);
                break;
            case SchemaStatus.Ahead:
                Ahead(logger);
                break;
            default:
                Unreachable(logger);
                break;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The database is ready: its schema is at the version this build expects.")]
    private static partial void Ready(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Not ready: the database schema is behind this build. Run `Keryx.Cli migrate`; this process never migrates.")]
    private static partial void Behind(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Not ready: the database schema was changed by a newer build than this one. Deploy that build.")]
    private static partial void Ahead(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Not ready: the database cannot be reached.")]
    private static partial void Unreachable(ILogger logger);
}
