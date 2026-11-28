using Keryx.Application.Abstractions;
using Keryx.Domain.Collection;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Alerts;

/// <summary>
/// Sends the mail service refused are one <c>alert_not_sent</c> warning per worker, continued while
/// they happen run after run (L2-020 AC1) and closed by the next send it takes. A run with nothing
/// to send leaves it open, since nothing shows the mail service has recovered.
/// </summary>
public sealed class AlertNotSentTracker(IAppDbContext db, TimeProvider time)
{
    public async Task TrackAsync(Run run, int notSent, int sent, string detail, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var open = await db.RunIssues
            .SingleOrDefaultAsync(issue => issue.Kind == IssueKind.AlertNotSent && issue.Worker == run.Worker && issue.State != IssueState.Closed, cancellationToken)
            .ConfigureAwait(false);
        if (notSent == 0)
        {
            if (sent > 0)
            {
                open?.Close();
            }

            return;
        }

        var previous = await db.Runs.AsNoTracking()
            .Where(other => other.Worker == run.Worker && other.Id != run.Id && other.StartedAt <= run.StartedAt)
            .OrderByDescending(other => other.StartedAt).ThenByDescending(other => other.Id)
            .Select(other => (long?)other.Id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var continues = open is not null && previous is { } previousId && await db.IssueOccurrences
            .AnyAsync(occurrence => occurrence.IssueId == open.Id && occurrence.RunId == previousId, cancellationToken).ConfigureAwait(false);
        if (continues)
        {
            open!.Continue(detail);
        }
        else
        {
            open?.Close();
            open = RunIssue.AlertNotSent(run.Worker, detail, time.GetUtcNow());
            db.RunIssues.Add(open);
        }

        db.IssueOccurrences.Add(IssueOccurrence.Of(open, run, time.GetUtcNow()));
    }
}
