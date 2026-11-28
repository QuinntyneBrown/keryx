using Keryx.Domain;
using Keryx.Domain.Alerts;

namespace Keryx.Application.Tests.Collection;

// Traces to: L2-078 AC1
// One case per row: the next 08:00 Toronto time after an instant, in UTC, across the changes of
// clock (8 March and 1 November 2026).
public sealed class DigestScheduleTests
{
    [Theory]
    [InlineData("2026-11-27T15:00:00Z", "2026-11-28T13:00:00Z", "10:00 EST: tomorrow")]
    [InlineData("2026-11-27T12:59:59Z", "2026-11-27T13:00:00Z", "07:59:59 EST: today")]
    [InlineData("2026-11-27T13:00:00Z", "2026-11-28T13:00:00Z", "08:00 exactly: the next day's")]
    [InlineData("2026-07-01T11:00:00Z", "2026-07-01T12:00:00Z", "07:00 EDT: today, 12:00 UTC")]
    [InlineData("2026-03-07T14:00:00Z", "2026-03-08T12:00:00Z", "the night clocks go forward")]
    [InlineData("2026-10-31T13:00:00Z", "2026-11-01T13:00:00Z", "the night clocks go back")]
    [InlineData("2026-12-31T20:00:00Z", "2027-01-01T13:00:00Z", "the year turns")]
    public void NextAfter(string now, string next, string because) =>
        Assert.True(
            DateTimeOffset.Parse(next, System.Globalization.CultureInfo.InvariantCulture)
                == DigestSchedule.NextAfter(DateTimeOffset.Parse(now, System.Globalization.CultureInfo.InvariantCulture), TorontoTime.Zone),
            because);
}
