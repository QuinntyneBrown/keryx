using Keryx.Domain.Catalogue;

namespace Keryx.Application.Tests.Catalogue;

/// <summary>An event's time in UTC with its zone (L2-016, L2-018), and when two events are one (L2-010 AC4).</summary>
public sealed class EventRulesTests
{
    private const string Toronto = "America/Toronto";

    [Theory]
    // A zoned time is converted as given.
    [InlineData("2026-12-05T09:00:00-05:00", "2026-12-05T14:00:00Z")]
    [InlineData("2026-12-05T14:00:00Z", "2026-12-05T14:00:00Z")]
    // A local time takes the region's zone, in winter and in summer.
    [InlineData("2026-12-05T09:00", "2026-12-05T14:00:00Z")]
    [InlineData("2026-07-05 09:00", "2026-07-05T13:00:00Z")]
    // A local time in the spring gap moves forward by the gap; one in the autumn overlap is its first occurrence.
    [InlineData("2027-03-14T02:30", "2027-03-14T07:30:00Z")]
    [InlineData("2026-11-01T01:30", "2026-11-01T05:30:00Z")]
    public void Given_a_published_start_When_it_is_read_in_Toronto_Then_it_is_stored_in_UTC_with_the_zone(string published, string utc)
    {
        var time = EventTime.From(published, Toronto)!;

        Assert.Equal(DateTimeOffset.Parse(utc, System.Globalization.CultureInfo.InvariantCulture), time.StartsAtUtc);
        Assert.Equal(TimeSpan.Zero, time.StartsAtUtc.Offset);
        Assert.Equal(Toronto, time.TimeZone);
    }

    [Fact]
    public void Given_a_zoned_start_in_a_region_outside_Toronto_When_it_is_read_Then_its_offset_decides_not_the_zone()
    {
        // The region's zone differs from the machine's and from the offset, so only the offset can give 08:00Z.
        var time = EventTime.From("2026-12-05T09:00:00+01:00", "Europe/London")!;

        Assert.Equal(new DateTimeOffset(2026, 12, 5, 8, 0, 0, TimeSpan.Zero), time.StartsAtUtc);
        Assert.Equal("Europe/London", time.TimeZone);
    }

    [Theory]
    [InlineData("")]
    [InlineData("soon")]
    [InlineData("2026-13-40T09:00")]
    public void Given_no_readable_start_When_it_is_read_Then_there_is_no_time(string published) => Assert.Null(EventTime.From(published, Toronto));

    [Theory]
    [InlineData("Trinity Bellwoods Park", 59, "Trinity Bellwoods Park", true)]
    [InlineData("Trinity Bellwoods Park", 60, " trinity  bellwoods park.", true)]
    [InlineData("Trinity Bellwoods Park", 61, "Trinity Bellwoods Park", false)]
    [InlineData("Trinity Bellwoods Park", -60, "Trinity Bellwoods Park", true)]
    [InlineData("Trinity Bellwoods Park", 0, "High Park", false)]
    public void Given_two_events_of_one_organization_When_they_are_compared_Then_they_are_one_within_an_hour_at_the_same_place(
        string place, int minutesApart, string otherPlace, bool same)
    {
        var start = new DateTimeOffset(2026, 12, 5, 14, 0, 0, TimeSpan.Zero);

        Assert.Equal(same, EventMatch.Same(start, place, start.AddMinutes(minutesApart), otherPlace));
    }
}
