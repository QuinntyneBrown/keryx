using Microsoft.EntityFrameworkCore;

namespace Keryx.Infrastructure.Persistence.Schema;

/// <summary>
/// Reads the schema's state from EF Core: whether the database answers, the last migration its
/// history holds and the last migration compiled into this build. A provider with no migrations
/// (the tests' EF Core InMemory) is always at the expected version.
/// </summary>
internal sealed class EfSchemaVersion(KeryxDbContext db) : ISchemaVersion
{
    public async Task<SchemaState> GetAsync(CancellationToken cancellationToken)
    {
        if (!db.Database.IsRelational())
        {
            return new SchemaState(Reachable: true, Applied: null, Expected: null);
        }

        var expected = db.Database.GetMigrations().LastOrDefault();
        try
        {
            if (!await db.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false))
            {
                return SchemaState.Unreachable(expected);
            }

            // On a database that was never migrated the provider finds no history table by
            // failing to read it: EF Core logs that failed command, and the answer is "none".
            var applied = await db.Database.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false);
            return new SchemaState(Reachable: true, applied.LastOrDefault(), expected);
        }
#pragma warning disable CA1031 // Any failure to read the database means not ready; its detail is never reported.
        catch (Exception exception) when (exception is not OperationCanceledException)
#pragma warning restore CA1031
        {
            return SchemaState.Unreachable(expected);
        }
    }
}
