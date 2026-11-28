using Keryx.Domain.Collection;

namespace Keryx.Domain.Alerts;

/// <summary>What an alert is about: a source that keeps failing, or a whole run that failed.</summary>
public enum AlertKind
{
    SourceFailing,
    RunFailed,
}

/// <summary>
/// One alert, about one issue or one run, with its attempts and when it was sent (L2-077). One
/// record per issue makes "once per streak" checkable, and its attempts "retried on the next run
/// at most".
/// </summary>
public sealed class AlertRecord
{
    private AlertRecord()
    {
    }

    public long Id { get; private set; }

    public AlertKind Kind { get; private set; }

    /// <summary>The issue a source-failing alert is about.</summary>
    public long? IssueId { get; private set; }

    /// <summary>The run a failed-run alert is about.</summary>
    public long? RunId { get; private set; }

    public int Attempts { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public static AlertRecord ForIssue(RunIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        return new AlertRecord { Kind = AlertKind.SourceFailing, IssueId = issue.Id };
    }

    public void Sent(DateTimeOffset now)
    {
        Attempts++;
        SentAt = now;
    }
}

/// <summary>Whether a source's issue raises its alert now (L2-077 AC1–AC3).</summary>
public static class SourceFailureAlertPolicy
{
    /// <summary>
    /// An open fetch failure whose streak has reached the threshold, with no alert recorded for it
    /// yet. A muted or closed issue never alerts, and a streak alerts once whatever its length.
    /// </summary>
    public static bool ShouldAlert(RunIssue issue, int threshold, AlertRecord? record)
    {
        ArgumentNullException.ThrowIfNull(issue);
        return record is null
            && issue.State == IssueState.Open
            && issue.Kind is IssueKind.SourceNotFound or IssueKind.SourceTimeout or IssueKind.SourceBlocked or IssueKind.SourceInvalid
            && issue.ConsecutiveRuns >= threshold;
    }
}
