using Keryx.Application.Clients;
using Keryx.Domain.Clients;

namespace Keryx.Infrastructure.Clients;

/// <summary>
/// The windows of this API instance, in memory: each instance limits on its own, so a deployment
/// of n instances allows a client up to n times its limit. Windows that have passed are forgotten
/// at most once a minute, so memory holds only the callers of the last minute.
/// </summary>
public sealed class InMemoryRateWindowStore : IRateWindowStore
{
    private readonly Dictionary<string, RateWindow> windows = new(StringComparer.Ordinal);
    private readonly Lock gate = new();
    private DateTimeOffset nextSweep = DateTimeOffset.MinValue;

    /// <summary>How many callers' windows are held: the measure of its memory.</summary>
    public int Callers
    {
        get
        {
            lock (gate)
            {
                return windows.Count;
            }
        }
    }

    public RateDecision TryTake(string caller, DateTimeOffset now, int limit)
    {
        lock (gate)
        {
            Sweep(now);
            if (!windows.TryGetValue(caller, out var window))
            {
                window = RateWindow.Open(now, limit);
                windows[caller] = window;
            }

            return window.TryTake(now);
        }
    }

    public bool IsFull(string caller, DateTimeOffset now)
    {
        lock (gate)
        {
            return windows.TryGetValue(caller, out var window) && window.IsFull(now);
        }
    }

    private void Sweep(DateTimeOffset now)
    {
        if (now < nextSweep)
        {
            return;
        }

        foreach (var passed in windows.Where(entry => entry.Value.HasPassed(now)).Select(entry => entry.Key).ToList())
        {
            windows.Remove(passed);
        }

        nextSweep = now + RateWindow.Length;
    }
}
