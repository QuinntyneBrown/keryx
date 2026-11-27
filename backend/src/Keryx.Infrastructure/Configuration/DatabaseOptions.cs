namespace Keryx.Infrastructure.Configuration;

/// <summary>The database a process uses. Documented in tools/deploy/configuration.md.</summary>
public sealed class DatabaseOptions
{
    /// <summary>The name of the connection string: the setting <c>ConnectionStrings__Keryx</c>.</summary>
    public const string ConnectionName = "Keryx";

    /// <summary>The PostgreSQL connection string. A secret: it is never logged or reported.</summary>
    public string? ConnectionString { get; set; }
}
