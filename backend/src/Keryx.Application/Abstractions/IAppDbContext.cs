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
    DbSet<Keryx.Domain.Staff.StaffSession> StaffSessions { get; }

    DbSet<Keryx.Domain.Staff.StaffMember> StaffMembers { get; }

    DbSet<Region> Regions { get; }

    DbSet<Category> Categories { get; }

    DbSet<Settings> Settings { get; }

    DbSet<Source> Sources { get; }

    DbSet<Keryx.Domain.Collection.Run> Runs { get; }

    DbSet<Keryx.Domain.Collection.RunLease> RunLeases { get; }

    DbSet<Keryx.Domain.Collection.RunIssue> RunIssues { get; }

    DbSet<Keryx.Domain.Collection.IssueOccurrence> IssueOccurrences { get; }

    DbSet<Keryx.Domain.Catalogue.Article> Articles { get; }

    DbSet<Keryx.Domain.Catalogue.StagedArticle> StagedArticles { get; }

    DbSet<Keryx.Domain.Catalogue.Organization> Organizations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
