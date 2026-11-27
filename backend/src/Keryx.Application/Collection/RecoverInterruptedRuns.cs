using Keryx.Application.Abstractions;
using Keryx.Domain.Collection;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Collection;

/// <summary>
/// Sent once by each worker when it starts, before any new run (L2-019 AC5): every run of that
/// worker still running with its execution started was cut short by a stop, so it is recorded
/// failed with reason <c>interrupted</c> and the worker's lease is freed. A run the pipeline has
/// not picked up yet is left to run.
/// </summary>
public sealed record RecoverInterruptedRuns(RunWorker Worker) : IRequest;

internal sealed class RecoverInterruptedRunsHandler(IAppDbContext db, TimeProvider time) : IRequestHandler<RecoverInterruptedRuns>
{
    internal const string Interrupted = "interrupted";

    public async Task Handle(RecoverInterruptedRuns request, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var lease = await db.RunLeases.SingleOrDefaultAsync(candidate => candidate.Worker == request.Worker, cancellationToken).ConfigureAwait(false);
        var cut = await db.Runs
            .Where(run => run.Worker == request.Worker && run.Outcome == RunOutcome.Running && run.ExecutionStarted)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var run in cut)
        {
            run.Fail(Interrupted, null, now);
            lease?.Release(run.PublicId);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
