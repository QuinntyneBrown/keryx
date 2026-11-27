namespace Keryx.Domain.Collection;

/// <summary>
/// When scheduled collection runs are due (L2-005): at each whole multiple of the interval after
/// local midnight, in the zone's wall-clock time. On a day the clocks change, slots keep their
/// wall-clock times. A wall-clock time that does not exist (spring forward) has no slot, and one
/// that happens twice (fall back) has one, at its first occurrence, so every slot that exists
/// runs exactly once.
/// </summary>
public static class CollectionSchedule
{
    /// <summary>The latest slot at or before <paramref name="now"/>.</summary>
    public static DateTimeOffset LatestSlotAtOrBefore(DateTimeOffset now, ScheduleInterval interval, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(interval);
        ArgumentNullException.ThrowIfNull(zone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        return SlotsAround(today, interval, zone).Last(slot => slot <= now);
    }

    /// <summary>The first slot after <paramref name="now"/>.</summary>
    public static DateTimeOffset NextSlotAfter(DateTimeOffset now, ScheduleInterval interval, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(interval);
        ArgumentNullException.ThrowIfNull(zone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        return SlotsAround(today, interval, zone).First(slot => slot > now);
    }

    /// <summary>The slots of the day before, the day and the day after, in order.</summary>
    private static IEnumerable<DateTimeOffset> SlotsAround(DateOnly day, ScheduleInterval interval, TimeZoneInfo zone)
    {
        for (var offset = -1; offset <= 1; offset++)
        {
            var date = day.AddDays(offset).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
            for (var hour = 0; hour < 24; hour += interval.Hours)
            {
                // A time the clocks skip takes the standard offset, which is the instant of the
                // next slot that exists, so it adds no slot of its own. A repeated time's first
                // occurrence is the one with the larger offset.
                var local = date.AddHours(hour);
                var utcOffset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
                yield return new DateTimeOffset(local, utcOffset);
            }
        }
    }
}
