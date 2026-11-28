namespace Keryx.Domain.Alerts;

/// <summary>When the daily digest is sent: each day at 08:00 Toronto time (L2-078 AC1).</summary>
public static class DigestSchedule
{
    public static readonly TimeSpan TimeOfDay = TimeSpan.FromHours(8);

    /// <summary>The first 08:00 in <paramref name="zone"/> after <paramref name="now"/>, in UTC.</summary>
    public static DateTimeOffset NextAfter(DateTimeOffset now, TimeZoneInfo zone) => DailySlot.NextAfter(now, zone, TimeOfDay);
}
