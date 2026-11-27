namespace Keryx.Domain.Collection;

/// <summary>
/// One row per worker naming the run in progress and the last schedule slot handled, so at most
/// one run of a worker is active and a slot never starts two runs (L2-005). A concurrent second
/// claim fails on <see cref="Version"/>.
/// </summary>
public sealed class RunLease
{
    private RunLease()
    {
    }

    public RunWorker Worker { get; private set; }

    /// <summary>The public id of the run in progress, or null when none is.</summary>
    public string? ActiveRunId { get; private set; }

    /// <summary>The latest slot already handled, by a run or by a skipped start.</summary>
    public DateTimeOffset LastScheduledSlotAt { get; private set; }

    /// <summary>Changes on every claim, release and slot, so concurrent writers conflict.</summary>
    public int Version { get; private set; }

    /// <summary>
    /// A worker's first lease. The slot it starts at counts as handled, so a worker started between
    /// slots waits for the next one rather than starting a run for a slot already past.
    /// </summary>
    public static RunLease For(RunWorker worker, DateTimeOffset handledSlot) =>
        new() { Worker = worker, LastScheduledSlotAt = handledSlot };

    public void Claim(string runId)
    {
        ActiveRunId = runId;
        Version++;
    }

    /// <summary>The run ended. Releasing a lease another run holds changes nothing.</summary>
    public void Release(string runId)
    {
        if (ActiveRunId == runId)
        {
            ActiveRunId = null;
            Version++;
        }
    }

    public void MarkSlotHandled(DateTimeOffset slot)
    {
        LastScheduledSlotAt = slot;
        Version++;
    }
}
