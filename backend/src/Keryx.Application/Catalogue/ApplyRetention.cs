using System.Linq.Expressions;
using Keryx.Application.Abstractions;
using Keryx.Application.Behaviors;
using Keryx.Domain.Catalogue;
using Keryx.Domain.Collection;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Catalogue;

/// <summary>The retention worker's daily request (L2-079).</summary>
public sealed record ApplyRetentionCommand(RunTrigger Trigger) : IRequest, IWorkerRunCommand
{
    public RunWorker Worker => RunWorker.Retention;
}

/// <summary>
/// Records a run with worker <c>retention</c>, reads the retention period staff set at its start,
/// and deletes what has expired in batches of at most <see cref="BatchSize"/> rows, each saved on
/// its own (L2-079). The run is finished with the number deleted: articles, events,
/// organizations, runs, issues, hours of API usage and audit entries; what goes with them is not
/// counted.
/// </summary>
internal sealed class ApplyRetentionCommandHandler(IAppDbContext db, TimeProvider time) : IRequestHandler<ApplyRetentionCommand>
{
    internal const int BatchSize = 1_000;

    public async Task Handle(ApplyRetentionCommand request, CancellationToken cancellationToken)
    {
        var run = Run.Start(RunWorker.Retention, request.Trigger, time.GetUtcNow());
        db.Runs.Add(run);
        var lease = await db.RunLeases.SingleOrDefaultAsync(candidate => candidate.Worker == RunWorker.Retention, cancellationToken).ConfigureAwait(false);
        if (lease is null)
        {
            lease = RunLease.For(RunWorker.Retention, time.GetUtcNow());
            db.RunLeases.Add(lease);
        }

        // The lease names the run, so a run that throws is still recorded (L2-019 AC3).
        lease.Claim(run.PublicId);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var months = await db.Settings.AsNoTracking().Select(settings => settings.RetentionMonths).SingleAsync(cancellationToken).ConfigureAwait(false);
        var cutoff = RetentionPolicy.Cutoff(run.StartedAt, months);
        // Events first: deleting an article sets its events' link to null.
        var deleted = await DeleteAsync(
            db.Events,
            item => (item.LinkedArticleId == null && item.StartsAtUtc < cutoff) || (item.LinkedArticle != null && item.LinkedArticle.CollectedAt < cutoff),
            cancellationToken).ConfigureAwait(false);
        deleted += await DeleteAsync(db.Articles, article => article.CollectedAt < cutoff, cancellationToken).ConfigureAwait(false);
        deleted += await DeleteOrganizationsAsync(cancellationToken).ConfigureAwait(false);
        deleted += await DeleteRunsAsync(cutoff, cancellationToken).ConfigureAwait(false);
        var usageCutoff = run.StartedAt - RetentionPolicy.UsageKept;
        deleted += await DeleteAsync(db.ApiUsageHours, hour => hour.HourStart < usageCutoff, cancellationToken).ConfigureAwait(false);
        deleted += await DeleteAsync(db.AuditEntries, entry => entry.OccurredAt < cutoff, cancellationToken).ConfigureAwait(false);

        run.Finish(new RunCounts { Deleted = deleted }, time.GetUtcNow());
        lease.Release(run.PublicId);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Organizations no article, event or source refers to. A source refers to the organization
    /// its organization name normalizes to; staff keep few sources, so their names are read whole.
    /// </summary>
    private async Task<int> DeleteOrganizationsAsync(CancellationToken cancellationToken)
    {
        var names = await db.Sources.AsNoTracking().Select(source => source.OrganizationName).ToListAsync(cancellationToken).ConfigureAwait(false);
        var named = names.Select(OrganizationName.Normalize).Distinct(StringComparer.Ordinal).ToList();
        return await DeleteAsync(
            db.Organizations,
            organization => !named.Contains(organization.NormalizedName)
                && !db.Articles.Any(article => article.OrganizationId == organization.Id)
                && !db.Events.Any(item => item.OrganizationId == organization.Id),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Finished runs started before the cutoff, after what hangs off them: their issue
    /// occurrences, staged items and alert records, each in batches of their own and not counted.
    /// Then closed issues left with no occurrence, after their alert records. A running run is
    /// never deleted, and an open or muted issue is kept.
    /// </summary>
    private async Task<int> DeleteRunsAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        Expression<Func<Run, bool>> runExpired = run => run.Outcome != RunOutcome.Running && run.StartedAt < cutoff;
        var expired = db.Runs.Where(runExpired).Select(run => run.Id);
        await DeleteAsync(db.IssueOccurrences, occurrence => expired.Contains(occurrence.RunId), cancellationToken).ConfigureAwait(false);
        await DeleteAsync(db.StagedArticles, staged => expired.Contains(staged.RunId), cancellationToken).ConfigureAwait(false);
        await DeleteAsync(db.AlertRecords, record => record.RunId != null && expired.Contains(record.RunId.Value), cancellationToken).ConfigureAwait(false);
        var deleted = await DeleteAsync(db.Runs, runExpired, cancellationToken).ConfigureAwait(false);

        Expression<Func<RunIssue, bool>> issueOrphaned = issue =>
            issue.State == IssueState.Closed && !db.IssueOccurrences.Any(occurrence => occurrence.IssueId == issue.Id);
        var orphaned = db.RunIssues.Where(issueOrphaned).Select(issue => issue.Id);
        await DeleteAsync(db.AlertRecords, record => record.IssueId != null && orphaned.Contains(record.IssueId.Value), cancellationToken).ConfigureAwait(false);
        return deleted + await DeleteAsync(db.RunIssues, issueOrphaned, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Deletes the rows that match, a batch at a time, oldest key first; returns how many.</summary>
    private async Task<int> DeleteAsync<T>(DbSet<T> rows, Expression<Func<T, bool>> expired, CancellationToken cancellationToken)
        where T : class
    {
        var total = 0;
        while (true)
        {
            var batch = await rows.Where(expired).OrderBy(row => EF.Property<long>(row, "Id")).Take(BatchSize)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            if (batch.Count == 0)
            {
                return total;
            }

            rows.RemoveRange(batch);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            total += batch.Count;
        }
    }
}
