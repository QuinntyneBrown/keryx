namespace Keryx.Domain.Collection;

/// <summary>The background jobs whose runs are recorded (L2-019).</summary>
public enum RunWorker
{
    Collection,
    Retention,
    AlertDigest,
}

/// <summary>What started a run.</summary>
public enum RunTrigger
{
    Schedule,
    Manual,
}

/// <summary>How a run ended, or that it has not (L2-019).</summary>
public enum RunOutcome
{
    Running,
    Succeeded,
    CompletedWithIssues,
    Failed,
}

/// <summary>The six stages of a collection run, in their order (L2-007).</summary>
public enum PipelineStage
{
    Fetch,
    Parse,
    Classify,
    Deduplicate,
    Store,
    Publish,
}

/// <summary>Where a stage got to (L2-007).</summary>
public enum StageStatus
{
    NotRun,
    Running,
    Done,
    DoneWithWarnings,
    Failed,
}
