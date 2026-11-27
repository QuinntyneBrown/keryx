using Keryx.Application.Abstractions;
using Keryx.Domain;
using Keryx.Domain.Collection;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Collection;

/// <summary>One look at the collection schedule (L2-005): start the run a slot is due for, or record its skip.</summary>
public sealed record TickCollectionSchedule : IRequest<ScheduleTickResult>;

/// <summary>When the schedule should be looked at next.</summary>
public sealed record ScheduleTickResult(DateTimeOffset NextWakeAt);

internal sealed class TickCollectionScheduleHandler(IAppDbContext db, TimeProvider time) : IRequestHandler<TickCollectionSchedule, ScheduleTickResult>
{
    /// <summary>
    /// The longest the schedule goes unread, so an interval saved in Settings takes effect at the
    /// next slot of the new interval.
    /// </summary>
    internal static readonly TimeSpan RecheckBound = TimeSpan.FromMinutes(1);

    public async Task<ScheduleTickResult> Handle(TickCollectionSchedule request, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var settings = await db.Settings.AsNoTracking().SingleAsync(cancellationToken).ConfigureAwait(false);
        var interval = ScheduleInterval.From(settings.IntervalHours);
        var slot = CollectionSchedule.LatestSlotAtOrBefore(now, interval, TorontoTime.Zone);
        var lease = await db.RunLeases.SingleOrDefaultAsync(candidate => candidate.Worker == RunWorker.Collection, cancellationToken).ConfigureAwait(false);
        if (lease is null)
        {
            db.RunLeases.Add(RunLease.For(RunWorker.Collection, slot));
        }
        else if (slot > lease.LastScheduledSlotAt)
        {
            if (lease.ActiveRunId is { } activeId)
            {
                var running = await db.Runs.SingleAsync(run => run.PublicId == activeId, cancellationToken).ConfigureAwait(false);
                var skipped = RunIssue.SkippedStart(RunWorker.Collection, now);
                db.RunIssues.Add(skipped);
                db.IssueOccurrences.Add(IssueOccurrence.Of(skipped, running, now));
            }
            else
            {
                var run = Run.Start(RunWorker.Collection, RunTrigger.Schedule, now);
                db.Runs.Add(run);
                lease.Claim(run.PublicId);
                // The skip is over once a scheduled run starts again.
                await foreach (var open in db.RunIssues
                    .Where(issue => issue.Kind == IssueKind.RunSkipped && issue.State != IssueState.Closed)
                    .AsAsyncEnumerable().WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    open.Close();
                }
            }

            lease.MarkSlotHandled(slot);
        }

        // A concurrent claim fails here on the lease's version, so a slot starts one run.
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var next = CollectionSchedule.NextSlotAfter(now, interval, TorontoTime.Zone);
        return new ScheduleTickResult(next < now + RecheckBound ? next : now + RecheckBound);
    }
}
