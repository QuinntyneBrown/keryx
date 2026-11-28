namespace Keryx.Domain.Collection;

/// <summary>
/// A finished run as it stood before its failed sources were fetched again: its counts and each
/// stage's status, duration and counts, which the retry's own results are merged into.
/// </summary>
public sealed record RetryBaseline(RunCounts Counts, IReadOnlyList<RetryBaseline.Stage> Stages)
{
    public sealed record Stage(PipelineStage Name, StageStatus Status, long DurationMs, IReadOnlyDictionary<string, int> Counts, string? Reason);
}
