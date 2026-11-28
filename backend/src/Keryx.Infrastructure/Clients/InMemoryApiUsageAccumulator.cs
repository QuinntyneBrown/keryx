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
        lock (gate)
        {
            var key = (clientId, ApiUsageHour.HourOf(at));
            if (!tallies.TryGetValue(key, out var tally))
            {
                tally = new Tally();
                tallies[key] = tally;
            }

            tally.Requests++;
            tally.Errors += failed ? 1 : 0;
            tally.Buckets[latencyBucket]++;
            tally.LastCallAt = at > tally.LastCallAt ? at : tally.LastCallAt;
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

    private sealed class Tally
    {
        public int Requests { get; set; }

        public int Errors { get; set; }

        public int[] Buckets { get; } = new int[LatencyBuckets.Count];

        public DateTimeOffset LastCallAt { get; set; } = DateTimeOffset.MinValue;
    }
}
