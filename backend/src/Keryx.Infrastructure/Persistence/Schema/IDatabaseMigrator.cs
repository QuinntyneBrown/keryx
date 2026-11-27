namespace Keryx.Infrastructure.Persistence.Schema;

/// <summary>
/// Changes the schema. Only the CLI's <c>migrate</c> command resolves it: the API and the worker
/// never migrate (L2-095).
/// </summary>
public interface IDatabaseMigrator
{
    /// <summary>Applies the pending migrations and returns how many it applied: none on a second run.</summary>
    Task<int> MigrateAsync(CancellationToken cancellationToken);
}
