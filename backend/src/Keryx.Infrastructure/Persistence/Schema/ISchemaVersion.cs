namespace Keryx.Infrastructure.Persistence.Schema;

/// <summary>
/// The one place that asks whether the database is at the schema this build expects (L2-095).
/// It never throws: a database that cannot be reached is a state, not a failure.
/// </summary>
public interface ISchemaVersion
{
    Task<SchemaState> GetAsync(CancellationToken cancellationToken);
}

/// <summary>Where the database's schema stands against the build.</summary>
public enum SchemaStatus
{
    /// <summary>Reachable and exactly at the version the build expects.</summary>
    Ready,

    /// <summary>Reachable, with migrations of this build still to apply: <c>migrate</c> must be run.</summary>
    Behind,

    /// <summary>Reachable, and migrated by a newer build than this one.</summary>
    Ahead,

    /// <summary>It did not answer.</summary>
    Unreachable,
}

/// <summary>
/// What the database holds against what the build expects: whether it could be reached, the last
/// migration applied to it and the last migration compiled into the build.
/// </summary>
public sealed record SchemaState(bool Reachable, string? Applied, string? Expected)
{
    /// <summary>
    /// A migration's id starts with its timestamp, so ids order as their migrations do. A
    /// database that holds no migration is behind any build that has one.
    /// </summary>
    public SchemaStatus Status => Reachable
        ? string.CompareOrdinal(Applied, Expected) switch
        {
            0 => SchemaStatus.Ready,
            < 0 => SchemaStatus.Behind,
            _ => SchemaStatus.Ahead,
        }
        : SchemaStatus.Unreachable;

    /// <summary>
    /// Ready only when the database is reachable and exactly at the expected version: a database
    /// behind the build is not ready, and neither is one ahead of it.
    /// </summary>
    public bool Ready => Status == SchemaStatus.Ready;

    /// <summary>A database that could not be reached.</summary>
    public static SchemaState Unreachable(string? expected) => new(Reachable: false, Applied: null, expected);
}
