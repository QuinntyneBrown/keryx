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
    public DbSet<Keryx.Domain.Staff.StaffSession> StaffSessions => Set<Keryx.Domain.Staff.StaffSession>();

    public DbSet<Keryx.Domain.Staff.StaffMember> StaffMembers => Set<Keryx.Domain.Staff.StaffMember>();

    public DbSet<Region> Regions => Set<Region>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Settings> Settings => Set<Settings>();

    public DbSet<Source> Sources => Set<Source>();

    public DbSet<Keryx.Domain.Collection.Run> Runs => Set<Keryx.Domain.Collection.Run>();

    public DbSet<Keryx.Domain.Collection.RunLease> RunLeases => Set<Keryx.Domain.Collection.RunLease>();

    public DbSet<Keryx.Domain.Collection.RunIssue> RunIssues => Set<Keryx.Domain.Collection.RunIssue>();

    public DbSet<Keryx.Domain.Collection.IssueOccurrence> IssueOccurrences => Set<Keryx.Domain.Collection.IssueOccurrence>();

    public DbSet<Keryx.Domain.Alerts.AlertRecord> AlertRecords => Set<Keryx.Domain.Alerts.AlertRecord>();

    public DbSet<Keryx.Domain.Catalogue.Article> Articles => Set<Keryx.Domain.Catalogue.Article>();

    public DbSet<Keryx.Domain.Catalogue.StagedArticle> StagedArticles => Set<Keryx.Domain.Catalogue.StagedArticle>();

    public DbSet<Keryx.Domain.Catalogue.Organization> Organizations => Set<Keryx.Domain.Catalogue.Organization>();

    public DbSet<Keryx.Domain.Catalogue.EventRecord> Events => Set<Keryx.Domain.Catalogue.EventRecord>();

    public DbSet<Keryx.Domain.Clients.ApiClient> ApiClients => Set<Keryx.Domain.Clients.ApiClient>();

    public DbSet<Keryx.Domain.Clients.ApiUsageHour> ApiUsageHours => Set<Keryx.Domain.Clients.ApiUsageHour>();

    public DbSet<Keryx.Domain.Audit.AuditEntry> AuditEntries => Set<Keryx.Domain.Audit.AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KeryxDbContext).Assembly);
    }
}
