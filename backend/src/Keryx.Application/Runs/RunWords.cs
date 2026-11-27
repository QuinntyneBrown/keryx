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
}
