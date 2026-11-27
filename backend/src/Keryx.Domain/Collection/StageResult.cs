namespace Keryx.Domain.Collection;

/// <summary>
/// One stage of one run (L2-007): its status, how long it took and a summary of named counts. A
/// failed stage carries a generic reason, never a host, address, connection string or exception text.
/// </summary>
public sealed class StageResult
{
    private StageResult()
    {
    }

    internal StageResult(PipelineStage stage) => Stage = stage;

    public PipelineStage Stage { get; private set; }

    public StageStatus Status { get; private set; } = StageStatus.NotRun;

    public long DurationMs { get; private set; }

    public IDictionary<string, int> Counts { get; private set; } = new Dictionary<string, int>();

    public string? Reason { get; private set; }

    internal void Record(StageStatus status, long durationMs, IReadOnlyDictionary<string, int> counts, string? reason)
    {
        Status = status;
        DurationMs = durationMs;
        Counts = new Dictionary<string, int>(counts);
        Reason = reason;
    }
}
