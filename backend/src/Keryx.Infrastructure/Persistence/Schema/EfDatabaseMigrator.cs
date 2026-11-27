using Microsoft.EntityFrameworkCore;

namespace Keryx.Infrastructure.Persistence.Schema;

/// <summary>Applies the migrations compiled into this build that the database does not hold yet.</summary>
internal sealed class EfDatabaseMigrator(KeryxDbContext db) : IDatabaseMigrator
{
    public async Task<int> MigrateAsync(CancellationToken cancellationToken)
    {
        var pending = await db.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false);
        var count = pending.Count();
        if (count > 0)
        {
            await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }

        return count;
    }
}
