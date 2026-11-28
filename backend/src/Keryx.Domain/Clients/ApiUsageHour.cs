namespace Keryx.Domain.Clients;

/// <summary>
/// One client's use of the consumer API in one hour: how many requests it made, how many of them
/// failed on Keryx's side, and how long they took, as counts per latency bucket (L2-037).
/// </summary>
public sealed class ApiUsageHour
{
    private ApiUsageHour()
    {
    }

    public long Id { get; private set; }

    /// <summary>The client's internal id.</summary>
    public long ClientId { get; private set; }

    /// <summary>The start of the hour, in UTC.</summary>
    public DateTimeOffset HourStart { get; private set; }

    public int Requests { get; private set; }

    public int Errors { get; private set; }

    /// <summary>Requests per bucket of <see cref="LatencyBuckets"/>, the last above its highest bound.</summary>
    public IReadOnlyList<int> LatencyBucketCounts { get; private set; } = new int[LatencyBuckets.Count];

    public static ApiUsageHour Start(long clientId, DateTimeOffset hourStart) =>
        new() { ClientId = clientId, HourStart = hourStart };

    /// <summary>Adds counts gathered since the last flush.</summary>
    public void Add(int requests, int errors, IReadOnlyList<int> latencyBucketCounts)
    {
        ArgumentNullException.ThrowIfNull(latencyBucketCounts);
        Requests += requests;
        Errors += errors;
        LatencyBucketCounts = [.. LatencyBucketCounts.Zip(latencyBucketCounts, (held, added) => held + added)];
    }

    /// <summary>The start of the hour an instant falls in, in UTC.</summary>
    public static DateTimeOffset HourOf(DateTimeOffset instant)
    {
        var utc = instant.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero);
    }

    /// <summary>
    /// The oldest hour in the last 24 hours: the current hour and the 23 before it, so the window
    /// never holds part of a 25th.
    /// </summary>
    public static DateTimeOffset WindowStart(DateTimeOffset now) => HourOf(now).AddHours(-23);
}

/// <summary>Every response is a request; a status from 500 to 599 is also an error (L2-037).</summary>
public static class ApiUsageClassifier
{
    public static bool IsError(int status) => status is >= 500 and <= 599;
}

/// <summary>
/// The latency buckets usage is counted in: upper bounds in milliseconds, and one bucket above the
/// highest. A p95 is read from them, since usage keeps counts only (L2-037).
/// </summary>
public static class LatencyBuckets
{
    public static IReadOnlyList<int> UpperBoundsMs { get; } = [10, 25, 50, 100, 250, 500, 1_000, 2_500, 5_000, 10_000];

    /// <summary>The bounded buckets and the one above them.</summary>
    public static int Count => UpperBoundsMs.Count + 1;

    /// <summary>The bucket a request of this duration falls in: the first whose bound it does not exceed.</summary>
    public static int IndexOf(TimeSpan elapsed)
    {
        for (var index = 0; index < UpperBoundsMs.Count; index++)
        {
            if (elapsed.TotalMilliseconds <= UpperBoundsMs[index])
            {
                return index;
            }
        }

        return UpperBoundsMs.Count;
    }
}
