using Keryx.Domain.Collection;

namespace Keryx.Application.Runs;

/// <summary>The words the API and the database use for runs and their stages.</summary>
public static class RunWords
{
    public static string Worker(RunWorker worker) => worker switch
    {
        RunWorker.Collection => "collection",
        RunWorker.Retention => "retention",
        RunWorker.AlertDigest => "alert_digest",
        _ => throw new ArgumentOutOfRangeException(nameof(worker)),
    };

    public static RunWorker WorkerOf(string word) => Enum.GetValues<RunWorker>().Single(value => Worker(value) == word);

    public static string Trigger(RunTrigger trigger) => trigger == RunTrigger.Manual ? "manual" : "schedule";

    public static RunTrigger TriggerOf(string word) => word == "manual" ? RunTrigger.Manual : RunTrigger.Schedule;

    public static string Outcome(RunOutcome outcome) => outcome switch
    {
        RunOutcome.Running => "running",
        RunOutcome.Succeeded => "succeeded",
        RunOutcome.CompletedWithIssues => "completed_with_issues",
        RunOutcome.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
    };

    public static RunOutcome OutcomeOf(string word) => Enum.GetValues<RunOutcome>().Single(value => Outcome(value) == word);

    public static string Stage(PipelineStage stage) => stage.ToString().ToLowerInvariant();

    public static PipelineStage StageOf(string word) => Enum.GetValues<PipelineStage>().Single(value => Stage(value) == word);

    public static string Status(StageStatus status) => status switch
    {
        StageStatus.NotRun => "not_run",
        StageStatus.Running => "running",
        StageStatus.Done => "done",
        StageStatus.DoneWithWarnings => "done_with_warnings",
        StageStatus.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    public static StageStatus StatusOf(string word) => Enum.GetValues<StageStatus>().Single(value => Status(value) == word);

    public static string Severity(IssueSeverity severity) => severity == IssueSeverity.Error ? "error" : "warning";

    public static IssueSeverity SeverityOf(string word) => word == "error" ? IssueSeverity.Error : IssueSeverity.Warning;

    public static string Kind(IssueKind kind) => kind switch
    {
        IssueKind.SourceNotFound => "source_not_found",
        IssueKind.SourceTimeout => "source_timeout",
        IssueKind.SourceBlocked => "source_blocked",
        IssueKind.SourceInvalid => "source_invalid",
        IssueKind.ItemsHeld => "items_held",
        IssueKind.StageFailed => "stage_failed",
        IssueKind.RunSkipped => "run_skipped",
        IssueKind.AlertNotSent => "alert_not_sent",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static IssueKind KindOf(string word) => Enum.GetValues<IssueKind>().Single(value => Kind(value) == word);

    public static string State(IssueState state) => state.ToString().ToLowerInvariant();

    public static IssueState StateOf(string word) => Enum.GetValues<IssueState>().Single(value => State(value) == word);
}
