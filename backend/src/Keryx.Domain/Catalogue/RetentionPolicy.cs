namespace Keryx.Domain.Catalogue;

/// <summary>How long Keryx keeps what it holds (L2-079, L2-037 AC5).</summary>
public static class RetentionPolicy
{
    /// <summary>
    /// The instant before which a record has expired: <paramref name="now"/> less the retention
    /// period, in UTC. A month with fewer days ends on its last day (31 March less a month is 28 or
    /// 29 February).
    /// </summary>
    public static DateTimeOffset Cutoff(DateTimeOffset now, int months)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(months);
        return now.ToUniversalTime().AddMonths(-months);
    }
}
