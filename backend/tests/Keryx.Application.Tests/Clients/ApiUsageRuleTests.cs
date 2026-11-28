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
