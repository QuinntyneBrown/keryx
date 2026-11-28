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

    public DbSet<Keryx.Domain.Collection.Run> Runs => inner.Runs;

    public DbSet<Keryx.Domain.Collection.RunLease> RunLeases => inner.RunLeases;

    public DbSet<Keryx.Domain.Collection.RunIssue> RunIssues => inner.RunIssues;

    public DbSet<Keryx.Domain.Collection.IssueOccurrence> IssueOccurrences => inner.IssueOccurrences;

    public DbSet<Keryx.Domain.Alerts.AlertRecord> AlertRecords => inner.AlertRecords;

    public DbSet<Keryx.Domain.Catalogue.Article> Articles => inner.Articles;

    public DbSet<Keryx.Domain.Catalogue.StagedArticle> StagedArticles => inner.StagedArticles;

    public DbSet<Keryx.Domain.Catalogue.Organization> Organizations => inner.Organizations;

    public DbSet<Keryx.Domain.Catalogue.EventRecord> Events => inner.Events;

    public DbSet<Keryx.Domain.Clients.ApiClient> ApiClients => inner.ApiClients;

    public DbSet<Keryx.Domain.Clients.ApiUsageHour> ApiUsageHours => fault.UsageReadArmed
        ? throw new InvalidOperationException("could not connect to server: Connection refused (10.0.0.12:5432)")
        : inner.ApiUsageHours;

    public DbSet<Keryx.Domain.Audit.AuditEntry> AuditEntries => inner.AuditEntries;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Another request claimed the lease between this one's read and its save: its run is saved
        // first, and this save then fails on the lease's version, as the database fails it.
        if (fault.LeaseRace is { } winner && inner.ChangeTracker.Entries<Keryx.Domain.Collection.Run>().Any(entry => entry.State == EntityState.Added))
        {
            fault.LeaseRace = null;
            await winner().ConfigureAwait(false);
            throw new DbUpdateConcurrencyException("The lease was changed by another request.");
        }

        // A save that deletes articles is a retention batch: its size is recorded, and the fault
        // fails it once the batches allowed have been saved.
        var deleting = inner.ChangeTracker.Entries<Keryx.Domain.Catalogue.Article>().Count(entry => entry.State == EntityState.Deleted);
        if (deleting > 0)
        {
            if (fault.ArticleDeletesBeforeFailing-- == 0)
            {
                throw new DbUpdateException("could not connect to server: Connection refused (10.0.0.12:5432)");
            }

            fault.ArticleDeletes.Add(deleting);
        }

        return await Saved(cancellationToken).ConfigureAwait(false);
    }

    private Task<int> Saved(CancellationToken cancellationToken) =>
        fault.Armed && (inner.ChangeTracker.Entries<Source>().Any(entry => entry.State == EntityState.Added)
            || inner.ChangeTracker.Entries<Keryx.Domain.Clients.ApiClient>().Any(entry => entry.State == EntityState.Added))
            ? throw new DbUpdateException("duplicate key value violates unique constraint")
            : fault.StoreArmed && inner.ChangeTracker.Entries<Keryx.Domain.Catalogue.StagedArticle>().Any(entry => entry.State == EntityState.Added)
                && fault.StoreSavesBeforeFailing-- <= 0
            ? throw new DbUpdateException("could not connect to server: Connection refused (10.0.0.12:5432)")
            : fault.UsageArmed && inner.ChangeTracker.Entries<Keryx.Domain.Clients.ApiUsageHour>().Any(entry => entry.State is EntityState.Added or EntityState.Modified)
            ? throw new DbUpdateException("could not connect to server: Connection refused (10.0.0.12:5432)")
            : inner.SaveChangesAsync(cancellationToken);

    /// <summary>Off while a test arranges its data; a test arms it before the request under test.</summary>
    public sealed class Switch
    {
        public bool Armed { get; set; }

        /// <summary>A save that stages a run's records fails, as when the database cannot be reached during the store stage.</summary>
        public bool StoreArmed { get; set; }

        /// <summary>A save of API usage fails, as when the database cannot be reached during a flush.</summary>
        public bool UsageArmed { get; set; }

        /// <summary>A read of API usage fails, as when the database drops during a flush, before its save.</summary>
        public bool UsageReadArmed { get; set; }

        /// <summary>How many saves that delete articles succeed before one fails; negative never fails.</summary>
        public int ArticleDeletesBeforeFailing { get; set; } = -1;

        /// <summary>How many articles each successful save that deleted any deleted, in order.</summary>
        public List<int> ArticleDeletes { get; } = [];

        /// <summary>How many saves that stage records succeed before the store fault fires.</summary>
        public int StoreSavesBeforeFailing { get; set; }

        /// <summary>
        /// The next save that adds a run loses a race for the lease: this runs the winning request's
        /// save, then the save fails on the lease's concurrency token.
        /// </summary>
        public Func<Task>? LeaseRace { get; set; }
    }
}
