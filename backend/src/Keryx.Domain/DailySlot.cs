namespace Keryx.Domain;

/// <summary>A time of day in a zone at which a worker runs once a day.</summary>
public static class DailySlot
{
    /// <summary>
    /// The first <paramref name="timeOfDay"/> in <paramref name="zone"/> after <paramref name="now"/>,
    /// in UTC. A time that a change of clock skips or repeats is the caller's to avoid: Toronto's
    /// clocks change at 02:00.
    /// </summary>
    public static DateTimeOffset NextAfter(DateTimeOffset now, TimeZoneInfo zone, TimeSpan timeOfDay)
    {
        ArgumentNullException.ThrowIfNull(zone);
        for (var day = TimeZoneInfo.ConvertTime(now, zone).Date; ; day = day.AddDays(1))
        {
            var at = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(day + timeOfDay, DateTimeKind.Unspecified), zone));
            if (at > now)
            {
                return at;
            }
        }
    }
}
