using System.Globalization;
using Keryx.Domain;
using Keryx.Domain.Catalogue;

namespace Keryx.Application.Tests.Catalogue;

// Traces to: L2-079 AC4
// One case per row: the next 03:00 Toronto time after an instant, in UTC, across the changes of
// clock (8 March and 1 November 2026).
public sealed class RetentionScheduleTests
{
    [Theory]
    [InlineData("2026-11-27T15:00:00Z", "2026-11-28T08:00:00Z", "10:00 EST: tomorrow")]
    [InlineData("2026-11-28T07:59:59Z", "2026-11-28T08:00:00Z", "02:59:59 EST: today")]
    [InlineData("2026-11-28T08:00:00Z", "2026-11-29T08:00:00Z", "03:00 exactly: the next day's")]
    [InlineData("2026-07-01T06:00:00Z", "2026-07-01T07:00:00Z", "02:00 EDT: today, 07:00 UTC")]
    [InlineData("2026-03-07T12:00:00Z", "2026-03-08T07:00:00Z", "the night clocks go forward")]
    [InlineData("2026-10-31T12:00:00Z", "2026-11-01T08:00:00Z", "the night clocks go back")]
    [InlineData("2026-12-31T20:00:00Z", "2027-01-01T08:00:00Z", "the year turns")]
    public void NextAfter(string now, string next, string because) =>
        Assert.True(
            DateTimeOffset.Parse(next, CultureInfo.InvariantCulture)
                == RetentionSchedule.NextAfter(DateTimeOffset.Parse(now, CultureInfo.InvariantCulture), TorontoTime.Zone),
            because);
}
