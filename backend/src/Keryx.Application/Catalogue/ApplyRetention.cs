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
/// its own (L2-079). The run is finished with the number deleted.
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
        var deleted = await DeleteAsync(db.Articles, article => article.CollectedAt < cutoff, cancellationToken).ConfigureAwait(false);

        run.Finish(new RunCounts { Deleted = deleted }, time.GetUtcNow());
        lease.Release(run.PublicId);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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
            if (batch.Count < BatchSize)
            {
                return total;
            }
        }
    }
}
