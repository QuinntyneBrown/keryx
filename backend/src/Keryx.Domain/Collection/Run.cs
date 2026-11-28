namespace Keryx.Domain.Collection;

/// <summary>
/// One run of one worker, however it ends (L2-019): what started it, when, how it ended, its six
/// stage results and its counts. Times are UTC.
/// </summary>
public sealed class Run
{
    private readonly List<StageResult> _stages = [];

    private Run()
    {
    }

    public long Id { get; private set; }

    /// <summary>The id staff see (<c>run_…</c>).</summary>
    public string PublicId { get; private set; } = string.Empty;

    public RunWorker Worker { get; private set; }

    public RunTrigger Trigger { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? FinishedAt { get; private set; }

    public long? DurationMs { get; private set; }

    public RunOutcome Outcome { get; private set; } = RunOutcome.Running;

    /// <summary>Why a failed run failed, in generic words.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>The trace id of a run that failed unexpectedly, to find it in telemetry.</summary>
    public string? CorrelationId { get; private set; }

    public RunCounts Counts { get; private set; } = new();

    public int SourcesChecked { get; private set; }

    public int SourcesTotal { get; private set; }

    /// <summary>Whether the pipeline has picked the run up; a run that has not cannot be interrupted.</summary>
    public bool ExecutionStarted { get; private set; }

    /// <summary>Staff asked for the run's failed sources to be fetched again; the worker has yet to finish it (L2-008 AC3).</summary>
    public bool RetryPending { get; private set; }

    public IReadOnlyCollection<StageResult> Stages => _stages;

    public static Run Start(RunWorker worker, RunTrigger trigger, DateTimeOffset now)
    {
        var run = new Run { PublicId = PublicIds.New("run"), Worker = worker, Trigger = trigger, StartedAt = now };
        run._stages.AddRange(Enum.GetValues<PipelineStage>().Select(stage => new StageResult(stage)));
        return run;
    }

    /// <summary>The pipeline picked the run up, with this many sources to check.</summary>
    public void BeginExecution(int sourcesTotal)
    {
        ExecutionStarted = true;
        SourcesTotal = sourcesTotal;
        SourcesChecked = 0;
    }

    /// <summary>
    /// Staff asked for the finished run's <paramref name="failedSources"/> failed sources to be
    /// fetched again: until the worker merges the retry, the run is running over those sources only.
    /// </summary>
    public void RequestRetry(int failedSources)
    {
        RetryPending = true;
        Outcome = RunOutcome.Running;
        ExecutionStarted = false;
        SourcesChecked = 0;
        SourcesTotal = failedSources;
    }

    /// <summary>The run as it stands before the worker fetches its failed sources again.</summary>
    public RetryBaseline RetryBaseline() =>
        new(Counts, [.. _stages.Select(stage => new RetryBaseline.Stage(stage.Stage, stage.Status, stage.DurationMs, new Dictionary<string, int>(stage.Counts), stage.Reason))]);

    /// <summary>
    /// The retry finished every stage over the failed sources (L2-008 AC3, AC4). Its counts are added
    /// to the run's; the sources that answered join those that did, and only those still failing
    /// stay failed. Each stage adds its counts and durations, and warns if either pass warned; the
    /// fetch stage warns while a source still fails. The outcome reads as if those sources had
    /// answered the first time, and the run keeps its original end.
    /// </summary>
    public void MergeRetry(RetryBaseline before, RunCounts retry)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(retry);
        Counts = new RunCounts
        {
            Added = before.Counts.Added + retry.Added,
            Updated = before.Counts.Updated + retry.Updated,
            Unchanged = before.Counts.Unchanged + retry.Unchanged,
            Duplicates = before.Counts.Duplicates + retry.Duplicates,
            Held = before.Counts.Held + retry.Held,
            OffTopic = before.Counts.OffTopic + retry.OffTopic,
            Rejected = before.Counts.Rejected + retry.Rejected,
            SourcesOk = before.Counts.SourcesOk + retry.SourcesOk,
            SourcesFailed = retry.SourcesFailed,
        };
        foreach (var earlier in before.Stages)
        {
            var stage = StageOf(earlier.Name);
            var counts = new Dictionary<string, int>(earlier.Counts);
            foreach (var (name, value) in stage.Counts)
            {
                counts[name] = counts.GetValueOrDefault(name) + value;
            }

            var status = earlier.Status == StageStatus.DoneWithWarnings || stage.Status == StageStatus.DoneWithWarnings
                ? StageStatus.DoneWithWarnings
                : StageStatus.Done;
            if (earlier.Name == PipelineStage.Fetch)
            {
                counts["sources_failed"] = retry.SourcesFailed;
                status = retry.SourcesFailed > 0 ? StageStatus.DoneWithWarnings : StageStatus.Done;
            }

            stage.Record(status, earlier.DurationMs + stage.DurationMs, counts, earlier.Reason);
        }

        SourcesTotal = SourcesChecked = Counts.SourcesOk + Counts.SourcesFailed;
        Outcome = Counts.SourcesFailed > 0 ? RunOutcome.CompletedWithIssues : RunOutcome.Succeeded;
        RetryPending = false;
    }

    /// <summary>One more source finished, whatever its result (L2-019 AC2).</summary>
    public void SourceChecked() => SourcesChecked++;

    public void RecordStage(PipelineStage stage, StageStatus status, long durationMs, IReadOnlyDictionary<string, int> counts, string? reason = null) =>
        StageOf(stage).Record(status, durationMs, counts, reason);

    /// <summary>
    /// The run finished every stage. It succeeded unless a source failed; held items are the
    /// classify stage's warning and leave the run succeeded (L2-007).
    /// </summary>
    public void Finish(RunCounts counts, DateTimeOffset now, bool withIssues = false)
    {
        ArgumentNullException.ThrowIfNull(counts);
        Counts = counts;
        Outcome = counts.SourcesFailed > 0 || withIssues ? RunOutcome.CompletedWithIssues : RunOutcome.Succeeded;
        End(now);
    }

    /// <summary>A stage failed: it carries the generic reason, every later stage is not run, and the run failed.</summary>
    public void FailStage(PipelineStage stage, string reason, DateTimeOffset now)
    {
        foreach (var later in _stages.Where(result => result.Stage > stage))
        {
            later.Record(StageStatus.NotRun, 0, new Dictionary<string, int>(), null);
        }

        StageOf(stage).Record(StageStatus.Failed, StageOf(stage).DurationMs, StageOf(stage).Counts.AsReadOnly(), reason);
        FailureReason = reason;
        Outcome = RunOutcome.Failed;
        End(now);
    }

    /// <summary>The run failed outside any stage, such as an unhandled exception (L2-019 AC3).</summary>
    public void Fail(string reason, string? correlationId, DateTimeOffset now)
    {
        FailureReason = reason;
        CorrelationId = correlationId;
        Outcome = RunOutcome.Failed;
        End(now);
    }

    private void End(DateTimeOffset now)
    {
        RetryPending = false;
        FinishedAt = now;
        DurationMs = (long)(now - StartedAt).TotalMilliseconds;
    }

    private StageResult StageOf(PipelineStage stage) => _stages.Single(result => result.Stage == stage);
}
