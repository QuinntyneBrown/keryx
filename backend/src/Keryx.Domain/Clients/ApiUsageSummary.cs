namespace Keryx.Domain.Clients;

/// <summary>Figures read from usage counts (L2-037).</summary>
public static class ApiUsageSummary
{
    /// <summary>Errors over requests, from 0 to 1; 0 when there were no requests.</summary>
    public static double ErrorRate(int requests, int errors) => requests == 0 ? 0 : (double)errors / requests;

    /// <summary>
    /// The 95th percentile latency in milliseconds, or null with no requests. Its rank is 0.95 ×
    /// requests; within the bucket holding that rank the value is interpolated between the
    /// bucket's bounds (the first starts at 0) and rounded half away from zero. A rank above the
    /// highest bound reads that bound, as Prometheus' <c>histogram_quantile</c> does.
    /// </summary>
    public static int? P95LatencyMs(IReadOnlyList<int> bucketCounts)
    {
        ArgumentNullException.ThrowIfNull(bucketCounts);
        var requests = bucketCounts.Sum();
        if (requests == 0)
        {
            return null;
        }

        var rank = 0.95 * requests;
        var below = 0;
        for (var index = 0; index < LatencyBuckets.UpperBoundsMs.Count; index++)
        {
            var count = bucketCounts[index];
            if (below + count >= rank)
            {
                var lower = index == 0 ? 0 : LatencyBuckets.UpperBoundsMs[index - 1];
                var upper = LatencyBuckets.UpperBoundsMs[index];
                return (int)Math.Round(lower + ((upper - lower) * (rank - below) / count), MidpointRounding.AwayFromZero);
            }

            below += count;
        }

        return LatencyBuckets.UpperBoundsMs[^1];
    }
}
