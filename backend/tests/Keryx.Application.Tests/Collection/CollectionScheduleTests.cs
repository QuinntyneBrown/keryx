using Keryx.Domain.Collection;

namespace Keryx.Application.Tests.Collection;

/// <summary>The slots of the collection schedule in Toronto wall-clock time (L2-005).</summary>
public sealed class CollectionScheduleTests
{
    private static readonly TimeZoneInfo Toronto = TimeZoneInfo.FindSystemTimeZoneById("America/Toronto");

    [Theory]
    // AC1: at 05:59:59 the latest slot is midnight; one second later it is 06:00.
    [InlineData(6, "2026-11-27T05:59:59-05:00", "2026-11-27T00:00:00-05:00")]
    [InlineData(6, "2026-11-27T06:00:00-05:00", "2026-11-27T06:00:00-05:00")]
    [InlineData(6, "2026-11-27T00:00:00-05:00", "2026-11-27T00:00:00-05:00")]
    [InlineData(6, "2026-11-26T23:59:59-05:00", "2026-11-26T18:00:00-05:00")]
    [InlineData(24, "2026-11-27T23:59:59-05:00", "2026-11-27T00:00:00-05:00")]
    [InlineData(3, "2026-11-27T09:46:00-05:00", "2026-11-27T09:00:00-05:00")]
    public void Given_a_time_When_the_latest_slot_is_read_Then_it_is_the_last_whole_multiple_of_the_interval_after_midnight(
        int hours, string now, string slot)
    {
        Assert.Equal(DateTimeOffset.Parse(slot, null), CollectionSchedule.LatestSlotAtOrBefore(DateTimeOffset.Parse(now, null), ScheduleInterval.From(hours), Toronto));
    }

    [Theory]
    // AC2 and AC3: at 09:46 the next slot is 12:00 for 6 hours and for 3 hours.
    [InlineData(6, "2026-11-27T09:46:00-05:00", "2026-11-27T12:00:00-05:00")]
    [InlineData(3, "2026-11-27T09:46:00-05:00", "2026-11-27T12:00:00-05:00")]
    [InlineData(3, "2026-11-27T12:00:00-05:00", "2026-11-27T15:00:00-05:00")]
    [InlineData(6, "2026-11-27T18:00:00-05:00", "2026-11-28T00:00:00-05:00")]
    [InlineData(12, "2026-11-27T13:00:00-05:00", "2026-11-28T00:00:00-05:00")]
    public void Given_a_time_When_the_next_slot_is_read_Then_it_is_the_first_slot_after_it(int hours, string now, string slot)
    {
        Assert.Equal(DateTimeOffset.Parse(slot, null), CollectionSchedule.NextSlotAfter(DateTimeOffset.Parse(now, null), ScheduleInterval.From(hours), Toronto));
    }

    [Theory]
    // AC4. 8 March 2026: 02:00 EST becomes 03:00 EDT, so 02:00 never happens.
    [InlineData(6, "2026-03-08", "2026-03-08T00:00:00-05:00,2026-03-08T06:00:00-04:00,2026-03-08T12:00:00-04:00,2026-03-08T18:00:00-04:00")]
    [InlineData(1, "2026-03-08", "2026-03-08T00:00:00-05:00,2026-03-08T01:00:00-05:00,2026-03-08T03:00:00-04:00,2026-03-08T04:00:00-04:00")]
    // 1 November 2026: 02:00 EDT becomes 01:00 EST, so 01:00 happens twice; the first one is the slot.
    [InlineData(6, "2026-11-01", "2026-11-01T00:00:00-04:00,2026-11-01T06:00:00-05:00,2026-11-01T12:00:00-05:00,2026-11-01T18:00:00-05:00")]
    [InlineData(1, "2026-11-01", "2026-11-01T00:00:00-04:00,2026-11-01T01:00:00-04:00,2026-11-01T02:00:00-05:00,2026-11-01T03:00:00-05:00")]
    public void Given_a_day_the_clocks_change_When_its_slots_are_listed_Then_they_follow_the_wall_clock_and_none_is_doubled(
        int hours, string day, string expected)
    {
        var interval = ScheduleInterval.From(hours);
        var at = CollectionSchedule.LatestSlotAtOrBefore(Midnight(day), interval, Toronto);
        var slots = new List<DateTimeOffset> { at };
        var wanted = expected.Split(',').Select(text => DateTimeOffset.Parse(text, null)).ToList();
        while (slots.Count < wanted.Count)
        {
            at = CollectionSchedule.NextSlotAfter(at, interval, Toronto);
            slots.Add(at);
        }

        Assert.Equal(wanted, slots);
    }

    [Fact]
    public void Given_the_1_hour_interval_When_a_day_with_23_or_25_hours_is_walked_Then_every_wall_clock_hour_that_exists_runs_once()
    {
        var interval = ScheduleInterval.From(1);
        foreach (var (day, count) in new[] { ("2026-03-08", 23), ("2026-11-01", 24), ("2026-11-27", 24) })
        {
            var next = Midnight(DateOnly.Parse(day, null).AddDays(1).ToString("yyyy-MM-dd", null));
            var seen = 0;
            for (var slot = Midnight(day); slot < next; slot = CollectionSchedule.NextSlotAfter(slot, interval, Toronto))
            {
                seen++;
            }

            Assert.Equal(count, seen);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(12)]
    [InlineData(24)]
    public void Given_an_allowed_interval_When_it_is_made_Then_it_keeps_its_hours(int hours) =>
        Assert.Equal(hours, ScheduleInterval.From(hours).Hours);

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(48)]
    [InlineData(-6)]
    public void Given_any_other_interval_When_it_is_made_Then_it_is_refused(int hours) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ScheduleInterval.From(hours));

    [Fact]
    public void Given_no_interval_chosen_When_the_default_is_read_Then_it_is_6_hours() =>
        Assert.Equal(6, ScheduleInterval.Default.Hours);

    /// <summary>Toronto's midnight on <paramref name="day"/>, which is never skipped or repeated.</summary>
    private static DateTimeOffset Midnight(string day)
    {
        var local = DateTime.Parse(day, null);
        return new DateTimeOffset(local, Toronto.GetUtcOffset(local));
    }
}
