using Keryx.Application.Abstractions;
using Keryx.Application.Collection;
using Keryx.Application.Errors;
using Keryx.Domain.Collection;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Runs;

/// <summary>The run a retry runs as: the same run, or a new manual run when the original failed.</summary>
public sealed record RetryStartedDto(string Id);

/// <summary>Fetches a finished collection run's failed sources again (L2-008 AC3, AC5).</summary>
public sealed record RetryFailedSources(string Id) : IRequest<RetryStartedDto>;

/// <summary>The run has no failed source to fetch again.</summary>
public sealed class NothingToRetryException()
    : KeryxException(409, "nothing_to_retry", "The run has no failed source to retry.");

/// <summary>
/// Claims the collection lease for the same run and marks it running over its failed sources: the
/// sources whose fetch issue, still open, occurred in that run. The worker then fetches only those
/// and merges what it finds into the run. A run that failed at a stage has no useful partial
/// result, so a new manual run starts instead, as "Run now" starts one.
/// </summary>
internal sealed class RetryFailedSourcesHandler(IAppDbContext db, ISender sender) : IRequestHandler<RetryFailedSources, RetryStartedDto>
{
    public async Task<RetryStartedDto> Handle(RetryFailedSources request, CancellationToken cancellationToken)
    {
        var run = await db.Runs.SingleOrDefaultAsync(candidate => candidate.PublicId == request.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new RunNotFoundException();
        if (run.Worker != RunWorker.Collection)
        {
            throw new NothingToRetryException();
        }

        var lease = await db.RunLeases.SingleOrDefaultAsync(candidate => candidate.Worker == RunWorker.Collection, cancellationToken).ConfigureAwait(false);
        if (lease?.ActiveRunId is { } running)
        {
            throw new RunInProgressException(running);
        }

        if (run.Outcome == RunOutcome.Failed)
        {
            var started = await sender.Send(new StartRun(), cancellationToken).ConfigureAwait(false);
            return new RetryStartedDto(started.Id);
        }

        var failed = await db.IssueOccurrences
            .Where(occurrence => occurrence.RunId == run.Id
                && occurrence.Issue.SourceId != null
                && occurrence.Issue.State != IssueState.Closed
                && SourceIssueTracker.FetchKinds.Contains(occurrence.Issue.Kind))
            .CountAsync(cancellationToken).ConfigureAwait(false);
        if (failed == 0 || lease is null)
        {
            throw new NothingToRetryException();
        }

        lease.Claim(run.PublicId);
        run.RequestRetry(failed);
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another start claimed the lease first: its run is the one in progress.
            var winner = await db.RunLeases
                .Where(candidate => candidate.Worker == RunWorker.Collection)
                .Select(candidate => candidate.ActiveRunId)
                .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (winner is null)
            {
                throw;
            }

            throw new RunInProgressException(winner);
        }

        return new RetryStartedDto(run.PublicId);
    }
}
