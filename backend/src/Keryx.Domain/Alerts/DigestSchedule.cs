namespace Keryx.Domain.Alerts;

/// <summary>When the daily digest is sent: each day at 08:00 Toronto time (L2-078 AC1).</summary>
public static class DigestSchedule
{
    public static readonly TimeSpan TimeOfDay = TimeSpan.FromHours(8);

    /// <summary>The first 08:00 in <paramref name="zone"/> after <paramref name="now"/>, in UTC.</summary>
    public static DateTimeOffset NextAfter(DateTimeOffset now, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        // 08:00 is never skipped or repeated in Toronto: its clocks change at 02:00.
        for (var day = TimeZoneInfo.ConvertTime(now, zone).Date; ; day = day.AddDays(1))
        {
            var at = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(day + TimeOfDay, DateTimeKind.Unspecified), zone));
            if (at > now)
            {
                return at;
            }
        }
    }
}
