using Keryx.Application.Abstractions;
using Keryx.Domain;
using Keryx.Domain.Reference;
using Keryx.Domain.Sources;
using Keryx.Domain.Staff;
using Keryx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Api.Tests.Fakes;

/// <summary>
/// The fixture's store, except that a save adding a source fails as the database fails it when another request
/// won a race to a unique index. EF Core InMemory enforces no unique index, so the fault is
/// injected here (AGENTS.md, "Backend tests").
/// </summary>
public sealed class SaveFault(KeryxDbContext inner, SaveFault.Switch fault) : IAppDbContext
{
    public DbSet<StaffSession> StaffSessions => inner.StaffSessions;

    public DbSet<StaffMember> StaffMembers => inner.StaffMembers;

    public DbSet<Region> Regions => inner.Regions;

    public DbSet<Category> Categories => inner.Categories;

    public DbSet<Settings> Settings => inner.Settings;

    public DbSet<Source> Sources => inner.Sources;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        fault.Armed && inner.ChangeTracker.Entries<Source>().Any(entry => entry.State == EntityState.Added)
            ? throw new DbUpdateException("duplicate key value violates unique constraint")
            : inner.SaveChangesAsync(cancellationToken);

    /// <summary>Off while a test arranges its data; a test arms it before the request under test.</summary>
    public sealed class Switch
    {
        public bool Armed { get; set; }
    }
}
