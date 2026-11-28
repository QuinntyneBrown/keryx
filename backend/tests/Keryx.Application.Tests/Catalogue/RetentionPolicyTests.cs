using System.Globalization;
using Keryx.Domain.Catalogue;

namespace Keryx.Application.Tests.Catalogue;

// Unit Test
// Traces to: L2-079 AC1, AC5
// Description: The cutoff is the clock less the retention period, in UTC; a month with fewer
// days ends on its last day. One case per row.
public sealed class RetentionPolicyTests
{
    [Theory]
    [InlineData("2026-11-27T15:00:00Z", 24, "2024-11-27T15:00:00Z")]
    [InlineData("2026-11-27T15:00:00Z", 1, "2026-10-27T15:00:00Z")]
    [InlineData("2026-11-27T15:00:00Z", 120, "2016-11-27T15:00:00Z")]
    [InlineData("2026-03-31T12:00:00Z", 1, "2026-02-28T12:00:00Z")]
    [InlineData("2028-03-31T12:00:00Z", 1, "2028-02-29T12:00:00Z")]
    [InlineData("2026-11-27T22:30:00-05:00", 12, "2025-11-28T03:30:00Z")]
    public void Given_a_clock_and_a_period_When_the_cutoff_is_worked_out_Then_it_is_the_clock_less_the_period_in_UTC(string now, int months, string cutoff)
    {
        var actual = RetentionPolicy.Cutoff(DateTimeOffset.Parse(now, CultureInfo.InvariantCulture), months);

        Assert.Equal(DateTimeOffset.Parse(cutoff, CultureInfo.InvariantCulture), actual);
        Assert.Equal(TimeSpan.Zero, actual.Offset);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Given_a_period_that_is_not_positive_When_the_cutoff_is_worked_out_Then_it_is_refused(int months) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => RetentionPolicy.Cutoff(DateTimeOffset.UnixEpoch, months));
}
