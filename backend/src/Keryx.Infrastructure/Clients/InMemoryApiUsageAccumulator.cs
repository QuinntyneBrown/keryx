using Keryx.Application.Clients;
using Keryx.Domain.Clients;

namespace Keryx.Infrastructure.Clients;

/// <summary>Usage counted in this process, per client and hour, until the next flush takes it.</summary>
internal sealed class InMemoryApiUsageAccumulator : IApiUsageAccumulator
{
    private readonly Lock gate = new();
    private Dictionary<(string ClientId, DateTimeOffset Hour), Tally> tallies = [];

    public void Add(string clientId, DateTimeOffset at, bool failed, int latencyBucket)
    {
        var buckets = new int[LatencyBuckets.Count];
        buckets[latencyBucket] = 1;
        lock (gate)
        {
            Merge(new ApiUsageTally(clientId, ApiUsageHour.HourOf(at), 1, failed ? 1 : 0, buckets, at));
        }
    }

    public IReadOnlyList<ApiUsageTally> Drain()
    {
        Dictionary<(string ClientId, DateTimeOffset Hour), Tally> taken;
        lock (gate)
        {
            taken = tallies;
            tallies = [];
        }

        return [.. taken.Select(pair => new ApiUsageTally(pair.Key.ClientId, pair.Key.Hour, pair.Value.Requests, pair.Value.Errors, pair.Value.Buckets, pair.Value.LastCallAt))];
    }

    public void Restore(IReadOnlyList<ApiUsageTally> tallies)
    {
        ArgumentNullException.ThrowIfNull(tallies);
        lock (gate)
        {
            foreach (var tally in tallies)
            {
                Merge(tally);
            }
        }
    }

    /// <summary>Adds counts to the client's tally for that hour; the caller holds the lock.</summary>
    private void Merge(ApiUsageTally counts)
    {
        var key = (counts.ClientId, counts.HourStart);
        if (!tallies.TryGetValue(key, out var tally))
        {
            tally = new Tally();
            tallies[key] = tally;
        }

        tally.Requests += counts.Requests;
        tally.Errors += counts.Errors;
        for (var index = 0; index < tally.Buckets.Length; index++)
        {
            tally.Buckets[index] += counts.LatencyBucketCounts[index];
        }

        tally.LastCallAt = counts.LastCallAt > tally.LastCallAt ? counts.LastCallAt : tally.LastCallAt;
    }

    private sealed class Tally
    {
        public int Requests { get; set; }

        public int Errors { get; set; }

        public int[] Buckets { get; } = new int[LatencyBuckets.Count];

        public DateTimeOffset LastCallAt { get; set; } = DateTimeOffset.MinValue;
    }
}
