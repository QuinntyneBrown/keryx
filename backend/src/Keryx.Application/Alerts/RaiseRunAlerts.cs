using Keryx.Application.Abstractions;
using Keryx.Domain.Alerts;
using Keryx.Domain.Collection;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Alerts;

/// <summary>A run was saved with its outcome; alerts follow it and cannot change it (L2-077).</summary>
public sealed record RunFinishedNotification(string RunId) : INotification;

/// <summary>
/// Raises a finished collection run's alerts to the alert address: one for each source whose
/// failure streak has just reached the threshold. With no alert address, or no mail service,
/// nothing is attempted.
/// </summary>
internal sealed class RaiseRunAlertsHandler(IAppDbContext db, IMailSender mail, AlertComposer composer, TimeProvider time)
    : INotificationHandler<RunFinishedNotification>
{
    public async Task Handle(RunFinishedNotification notification, CancellationToken cancellationToken)
    {
        var settings = await db.Settings.SingleAsync(cancellationToken).ConfigureAwait(false);
        if (!mail.IsConfigured || string.IsNullOrWhiteSpace(settings.AlertEmail))
        {
            return;
        }

        var run = await db.Runs.SingleAsync(candidate => candidate.PublicId == notification.RunId, cancellationToken).ConfigureAwait(false);
        if (run.Worker != RunWorker.Collection)
        {
            return;
        }

        var issues = await db.IssueOccurrences
            .Where(occurrence => occurrence.RunId == run.Id && occurrence.Issue.SourceId != null)
            .Select(occurrence => occurrence.Issue)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var issue in issues)
        {
            var record = await db.AlertRecords.SingleOrDefaultAsync(candidate => candidate.IssueId == issue.Id, cancellationToken).ConfigureAwait(false);
            if (!SourceFailureAlertPolicy.ShouldAlert(issue, settings.AlertFailureStreak, record))
            {
                continue;
            }

            var source = await db.Sources.SingleAsync(candidate => candidate.Id == issue.SourceId, cancellationToken).ConfigureAwait(false);
            record = AlertRecord.ForIssue(issue);
            db.AlertRecords.Add(record);
            await mail.SendAsync(composer.SourceFailing(settings.AlertEmail, source, issue, run), cancellationToken).ConfigureAwait(false);
            record.Sent(time.GetUtcNow());
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
