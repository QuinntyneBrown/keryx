using Keryx.Application.Abstractions;
using Keryx.Domain;
using Keryx.Domain.Reference;
using Keryx.Domain.Sources;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Infrastructure.Persistence;

/// <summary>
/// The database that holds everything Keryx knows (PostgreSQL, ADR 0001). Its schema changes
/// only through the migrations beside it, applied by <c>Keryx.Cli migrate</c>.
/// </summary>
public sealed class KeryxDbContext(DbContextOptions<KeryxDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Region> Regions => Set<Region>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Settings> Settings => Set<Settings>();

    public DbSet<Source> Sources => Set<Source>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KeryxDbContext).Assembly);
    }
}
