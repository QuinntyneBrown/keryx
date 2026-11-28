using Keryx.Application.Abstractions;
using Keryx.Domain.Alerts;
using Keryx.Domain.Collection;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Keryx.Application.Alerts;

/// <summary>A run was saved with its outcome; alerts follow it and cannot change it (L2-077).</summary>
public sealed record RunFinishedNotification(string RunId) : INotification;

/// <summary>
/// Raises a finished collection run's alerts to the alert address: first the alerts the previous
/// run could not send, once more; then one for each source whose failure streak has just reached
/// the threshold, and one for the run itself when it failed and "When a whole run fails" is on.
/// A send the mail service refuses becomes an <c>alert_not_sent</c> warning on the run, whose
/// outcome stays as it was (AC5). With no alert address, or no mail service, nothing is
/// attempted.
/// </summary>
internal sealed partial class RaiseRunAlertsHandler(
    IAppDbContext db, IMailSender mail, AlertComposer composer, AlertNotSentTracker tracker, TimeProvider time, ILogger<RaiseRunAlertsHandler> logger)
    : INotificationHandler<RunFinishedNotification>
{
    internal const string NotSentDetail = "An alert e-mail could not be sent. Keryx tries it once more on the next run.";

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

        var to = settings.AlertEmail;
        var outbox = new List<(AlertRecord Record, MailMessage Message)>();
        // Each alert the previous run could not send is tried once more, as things stand now.
        var pending = await db.AlertRecords.Where(record => record.SentAt == null).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var record in pending.Where(record => record.RetryDue))
        {
            if (await ComposeAgainAsync(record, to, run, cancellationToken).ConfigureAwait(false) is { } message)
            {
                outbox.Add((record, message));
            }
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
            outbox.Add((record, composer.SourceFailing(to, source, issue, run)));
        }

        // A failed run is never executed again (a retry of one is a new run), so it alerts once.
        if (run.Outcome == RunOutcome.Failed && settings.AlertOnRunFailure)
        {
            var record = AlertRecord.ForRun(run);
            db.AlertRecords.Add(record);
            outbox.Add((record, composer.RunFailed(to, run)));
        }

        var notSent = 0;
        foreach (var (record, message) in outbox)
        {
            try
            {
                await mail.SendAsync(message, cancellationToken).ConfigureAwait(false);
                record.Sent(time.GetUtcNow());
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                // The type only: a mail server's message may name its host or the address.
                AlertNotSent(logger, exception.GetType().Name);
                record.Failed();
                notSent++;
            }
        }

        await tracker.TrackAsync(run, notSent, outbox.Count - notSent, NotSentDetail, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The alert again, about its issue as it is now, or its run; none for an issue no longer open.</summary>
    private async Task<MailMessage?> ComposeAgainAsync(AlertRecord record, string to, Run latest, CancellationToken cancellationToken)
    {
        if (record.RunId is { } runId)
        {
            return composer.RunFailed(to, await db.Runs.SingleAsync(candidate => candidate.Id == runId, cancellationToken).ConfigureAwait(false));
        }

        var issue = await db.RunIssues.SingleAsync(candidate => candidate.Id == record.IssueId, cancellationToken).ConfigureAwait(false);
        if (issue.State != IssueState.Open)
        {
            return null;
        }

        var source = await db.Sources.SingleAsync(candidate => candidate.Id == issue.SourceId, cancellationToken).ConfigureAwait(false);
        return composer.SourceFailing(to, source, issue, latest);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "An alert e-mail could not be sent: {ExceptionType}.")]
    private static partial void AlertNotSent(ILogger logger, string exceptionType);
}
