// Acceptance Test
// Traces to: L2-095
// Description: The migrations compiled into the build produce the schema the model needs, so `migrate` leaves nothing behind

using Keryx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Tests.Schema;

public sealed class MigrationsTests
{
    // The connection string names the provider and reaches nothing: comparing the model with
    // the migrations' snapshot opens no connection.
    private static KeryxDbContext PostgreSql() =>
        new(new DbContextOptionsBuilder<KeryxDbContext>().UseNpgsql("Host=unused.invalid;Database=unused").Options);

    [Fact]
    public async Task Given_the_model_When_it_is_compared_with_the_migrations_Then_no_change_is_left_without_a_migration()
    {
        await using var db = PostgreSql();

        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Given_the_build_Then_its_expected_schema_version_is_its_last_migration()
    {
        await using var db = PostgreSql();

        var migrations = db.Database.GetMigrations().ToList();

        Assert.NotEmpty(migrations);
        Assert.EndsWith("_Initial", migrations[0], StringComparison.Ordinal);
        Assert.Equal(migrations.Order(StringComparer.Ordinal), migrations);
    }
}
