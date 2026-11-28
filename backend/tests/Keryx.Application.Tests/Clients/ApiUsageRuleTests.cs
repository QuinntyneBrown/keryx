using Keryx.Domain.Clients;

namespace Keryx.Application.Tests.Clients;

// Unit Test
// Traces to: L2-037 AC2
// Description: Every response is a request; only a 5xx is also an error. A request falls in the
// first latency bucket whose bound it does not exceed, and past the highest in the bucket above.
public sealed class ApiUsageRuleTests
{
    [Theory]
    [InlineData(200, false)]
    [InlineData(304, false)]
    [InlineData(400, false)]
    [InlineData(404, false)]
    [InlineData(429, false)]
    [InlineData(499, false)]
    [InlineData(500, true)]
    [InlineData(503, true)]
    [InlineData(599, true)]
    [InlineData(600, false)]
    public void A_status_is_an_error_only_from_500_to_599(int status, bool error) =>
        Assert.Equal(error, ApiUsageClassifier.IsError(status));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(10, 0)]
    [InlineData(10.5, 1)]
    [InlineData(25, 1)]
    [InlineData(84, 3)]
    [InlineData(100, 3)]
    [InlineData(250, 4)]
    [InlineData(999, 6)]
    [InlineData(5_000, 8)]
    [InlineData(10_000, 9)]
    [InlineData(10_001, 10)]
    public void A_request_falls_in_the_first_bucket_whose_bound_it_does_not_exceed(double milliseconds, int bucket) =>
        Assert.Equal(bucket, LatencyBuckets.IndexOf(TimeSpan.FromMilliseconds(milliseconds)));

    // The 95th percentile's rank is 0.95 × requests; within the bucket holding it the value is
    // interpolated between the bucket's bounds and rounded half away from zero. Buckets: ≤10, ≤25,
    // ≤50, ≤100, ≤250, ≤500, ≤1000, ≤2500, ≤5000, ≤10000 ms, and above.
    [Theory]
    [InlineData(new int[] { }, null)]
    [InlineData(new[] { 1, 0, 8, 0, 0, 1 }, 375)]
    [InlineData(new[] { 0, 0, 19, 0, 0, 1 }, 50)]
    [InlineData(new[] { 10 }, 10)]
    [InlineData(new[] { 0, 10 }, 24)]
    [InlineData(new[] { 47, 3 }, 13)] // rank 47.5, half-way between 12 and 13: half away from zero
    [InlineData(new[] { 0, 0, 0, 1 }, 98)]
    [InlineData(new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 100 }, 10_000)]
    [InlineData(new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 99 }, 10_000)]
    public void The_p95_is_read_from_the_bucket_holding_its_rank(int[] counts, int? p95)
    {
        var buckets = new int[LatencyBuckets.Count];
        counts.CopyTo(buckets, 0);

        Assert.Equal(p95, ApiUsageSummary.P95LatencyMs(buckets));
    }

    [Theory]
    [InlineData(0, 0, 0.0)]
    [InlineData(10, 1, 0.1)]
    [InlineData(4, 4, 1.0)]
    public void The_error_rate_is_errors_over_requests_and_0_with_none(int requests, int errors, double rate) =>
        Assert.Equal(rate, ApiUsageSummary.ErrorRate(requests, errors));

    [Fact]
    public void A_call_counted_late_never_moves_the_last_call_back()
    {
        var client = ApiClient.Create("Keryx Mobile", [ApiScopes.ArticlesRead], "kx_live_0123456789abcdef", "hash", DateTimeOffset.UnixEpoch);
        var later = new DateTimeOffset(2026, 11, 28, 15, 1, 0, TimeSpan.Zero);

        client.Called(later);
        client.Called(later.AddMinutes(-2));

        Assert.Equal(later, client.LastCallAt);
    }

    [Theory]
    [InlineData("2026-11-28T14:46:00Z", "2026-11-27T15:00:00Z")]
    [InlineData("2026-11-28T14:00:00Z", "2026-11-27T15:00:00Z")]
    [InlineData("2026-11-28T13:59:59Z", "2026-11-27T14:00:00Z")]
    [InlineData("2026-11-28T09:46:00-05:00", "2026-11-27T15:00:00Z")]
    public void The_window_is_the_current_hour_and_the_23_before_it(string now, string start) =>
        Assert.Equal(DateTimeOffset.Parse(start, System.Globalization.CultureInfo.InvariantCulture),
            ApiUsageHour.WindowStart(DateTimeOffset.Parse(now, System.Globalization.CultureInfo.InvariantCulture)));
}
