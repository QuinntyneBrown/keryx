namespace Keryx.Domain.Catalogue;

/// <summary>
/// When retention runs: each night at 03:00 Toronto time (L2-079 AC4), outside the 08:00 digest
/// and the working day. 03:00 is never skipped or repeated in Toronto.
/// </summary>
public static class RetentionSchedule
{
    public static readonly TimeSpan TimeOfDay = TimeSpan.FromHours(3);

    /// <summary>The first 03:00 in <paramref name="zone"/> after <paramref name="now"/>, in UTC.</summary>
    public static DateTimeOffset NextAfter(DateTimeOffset now, TimeZoneInfo zone) => DailySlot.NextAfter(now, zone, TimeOfDay);
}
