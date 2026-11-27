namespace Keryx.Domain.Collection;

/// <summary>How serious an issue is (L2-020).</summary>
public enum IssueSeverity
{
    Error,
    Warning,
}

/// <summary>What kind of problem an issue is (L2-020).</summary>
public enum IssueKind
{
    SourceNotFound,
    SourceTimeout,
    SourceBlocked,
    SourceInvalid,
    ItemsHeld,
    StageFailed,
    RunSkipped,
}

/// <summary>Whether an issue is open, muted by staff, or over.</summary>
public enum IssueState
{
    Open,
    Muted,
    Closed,
}

/// <summary>
/// A problem found during runs (L2-020): its severity and kind, what it concerns (a source, a
/// stage, or the worker itself), a short detail, and how many runs in a row it has occurred.
/// </summary>
public sealed class RunIssue
{
    private RunIssue()
    {
    }

    public long Id { get; private set; }

    /// <summary>The id staff see (<c>iss_…</c>).</summary>
    public string PublicId { get; private set; } = string.Empty;

    public RunWorker Worker { get; private set; }

    public IssueSeverity Severity { get; private set; }

    public IssueKind Kind { get; private set; }

    /// <summary>The source it concerns, when it concerns one.</summary>
    public long? SourceId { get; private set; }

    /// <summary>The stage it concerns, when it concerns one.</summary>
    public PipelineStage? Stage { get; private set; }

    /// <summary>The latest detail, already bounded and stripped of anything internal.</summary>
    public string Detail { get; private set; } = string.Empty;

    public int ConsecutiveRuns { get; private set; }

    /// <summary>When it first occurred in this streak, in UTC.</summary>
    public DateTimeOffset Since { get; private set; }

    public IssueState State { get; private set; } = IssueState.Open;

    /// <summary>A scheduled start arrived while a run was in progress (L2-005 AC5).</summary>
    public static RunIssue SkippedStart(RunWorker worker, DateTimeOffset now) => new()
    {
        PublicId = PublicIds.New("iss"),
        Worker = worker,
        Severity = IssueSeverity.Warning,
        Kind = IssueKind.RunSkipped,
        Detail = "A scheduled start was skipped because this run was still in progress.",
        ConsecutiveRuns = 1,
        Since = now,
    };

    /// <summary>A problem with one source, first seen now (L2-020).</summary>
    public static RunIssue ForSource(RunWorker worker, IssueSeverity severity, IssueKind kind, long sourceId, string detail, DateTimeOffset now) => new()
    {
        PublicId = PublicIds.New("iss"),
        Worker = worker,
        Severity = severity,
        Kind = kind,
        SourceId = sourceId,
        Detail = detail,
        ConsecutiveRuns = 1,
        Since = now,
    };

    /// <summary>It happened again in the next run: the streak grows and <see cref="Since"/> stays (L2-020 AC1).</summary>
    public void Continue(string detail)
    {
        ConsecutiveRuns++;
        Detail = detail;
    }

    /// <summary>The problem is over; a later recurrence is a new issue. Closing clears a mute.</summary>
    public void Close() => State = IssueState.Closed;
}

/// <summary>One issue in one run, with that run's detail, so an issue is listed on every run that raised it.</summary>
public sealed class IssueOccurrence
{
    private IssueOccurrence()
    {
    }

    public long Id { get; private set; }

    public long IssueId { get; private set; }

    public RunIssue Issue { get; private set; } = null!;

    public long RunId { get; private set; }

    public Run Run { get; private set; } = null!;

    public string Detail { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    public static IssueOccurrence Of(RunIssue issue, Run run, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(issue);
        ArgumentNullException.ThrowIfNull(run);
        return new IssueOccurrence { Issue = issue, Run = run, Detail = issue.Detail, OccurredAt = now };
    }
}
