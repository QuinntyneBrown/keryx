using Keryx.Application.Abstractions;
using Keryx.Application.Collection;
using Keryx.Application.Errors;
using Keryx.Domain;
using Keryx.Domain.Collection;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Runs;

/// <summary>
/// A run staff started: its id, how many sources it will check and how long the last finished run
/// took, for "Checking 64 sources. This usually takes about 4 minutes." (L2-047 AC1). The
/// duration is null when no run has finished.
/// </summary>
public sealed record RunStartedDto(string Id, int SourcesTotal, long? EstimatedDurationMs);

/// <summary>Starts a collection run now, unless one is in progress (L2-006).</summary>
public sealed record StartRun : IRequest<RunStartedDto>;

/// <summary>A collection run is already in progress; the client follows it by <c>run_id</c>.</summary>
public sealed class RunInProgressException(string runId)
    : KeryxException(409, "run_in_progress", "A collection run is already in progress.")
{
    public override IReadOnlyDictionary<string, object?> Extensions { get; } =
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["run_id"] = runId };
}

/// <summary>
/// Claims the collection lease for a new manual run, which the worker then executes as it would a
/// scheduled one. It never moves the schedule: the lease's last handled slot is left as it is.
/// </summary>
internal sealed class StartRunHandler(IAppDbContext db, TimeProvider time) : IRequestHandler<StartRun, RunStartedDto>
{
    public async Task<RunStartedDto> Handle(StartRun request, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var lease = await db.RunLeases.SingleOrDefaultAsync(candidate => candidate.Worker == RunWorker.Collection, cancellationToken).ConfigureAwait(false);
        if (lease is null)
        {
            // As the worker's first look at the schedule would: the slot already past counts as handled.
            var settings = await db.Settings.AsNoTracking().SingleAsync(cancellationToken).ConfigureAwait(false);
            var slot = CollectionSchedule.LatestSlotAtOrBefore(now, ScheduleInterval.From(settings.IntervalHours), TorontoTime.Zone);
            lease = RunLease.For(RunWorker.Collection, slot);
            db.RunLeases.Add(lease);
        }
        else if (lease.ActiveRunId is { } running)
        {
            throw new RunInProgressException(running);
        }

        var run = Run.Start(RunWorker.Collection, RunTrigger.Manual, now);
        db.Runs.Add(run);
        lease.Claim(run.PublicId);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var sources = await db.Sources.CountAsync(source => CollectionPipeline.Fetchable.Contains(source.Kind), cancellationToken).ConfigureAwait(false);
        var last = await db.Runs.AsNoTracking()
            .Where(candidate => candidate.Worker == RunWorker.Collection
                && (candidate.Outcome == RunOutcome.Succeeded || candidate.Outcome == RunOutcome.CompletedWithIssues))
            .OrderByDescending(candidate => candidate.StartedAt)
            .Select(candidate => candidate.DurationMs)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return new RunStartedDto(run.PublicId, sources, last);
    }
}
