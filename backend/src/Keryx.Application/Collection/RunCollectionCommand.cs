using Keryx.Application.Abstractions;
using Keryx.Application.Behaviors;
using Keryx.Domain.Collection;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Collection;

/// <summary>Runs the pending collection run, if there is one (L2-007).</summary>
public sealed record RunCollectionCommand : IRequest, IWorkerRunCommand
{
    public RunWorker Worker => RunWorker.Collection;
}

/// <summary>
/// Picks up the run the collection lease names when the pipeline has not started it, runs the
/// pipeline, finishes the run and frees the lease. With no pending run it returns at once.
/// </summary>
internal sealed class RunCollectionCommandHandler(IAppDbContext db, CollectionPipeline pipeline, TimeProvider time) : IRequestHandler<RunCollectionCommand>
{
    public async Task Handle(RunCollectionCommand request, CancellationToken cancellationToken)
    {
        var lease = await db.RunLeases.SingleOrDefaultAsync(candidate => candidate.Worker == RunWorker.Collection, cancellationToken).ConfigureAwait(false);
        if (lease?.ActiveRunId is not { } runId)
        {
            return;
        }

        var run = await db.Runs.SingleAsync(candidate => candidate.PublicId == runId, cancellationToken).ConfigureAwait(false);
        if (run.ExecutionStarted)
        {
            return;
        }

        // A retry is merged into what the run found before it (L2-008 AC3).
        var baseline = run.RetryPending ? run.RetryBaseline() : null;
        var counts = await pipeline.ExecuteAsync(run, cancellationToken).ConfigureAwait(false);
        // A stage that failed has already ended the run.
        if (run.Outcome == RunOutcome.Running)
        {
            if (baseline is null)
            {
                run.Finish(counts, time.GetUtcNow());
            }
            else
            {
                run.MergeRetry(baseline, counts);
            }
        }

        lease.Release(run.PublicId);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
