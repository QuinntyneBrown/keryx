using Keryx.Domain;
using Keryx.Domain.Reference;
using Keryx.Domain.Sources;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Abstractions;

/// <summary>
/// What a handler may read and change: everything Keryx knows. Infrastructure implements it with
/// EF Core; a test injects a fault through it.
/// </summary>
public interface IAppDbContext
{
    DbSet<Region> Regions { get; }

    DbSet<Category> Categories { get; }

    DbSet<Settings> Settings { get; }

    DbSet<Source> Sources { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
