namespace Keryx.Domain.Collection;

/// <summary>Hours between scheduled collection runs: 1, 3, 6, 12 or 24 (L2-005).</summary>
public sealed record ScheduleInterval
{
    private ScheduleInterval(int hours) => Hours = hours;

    /// <summary>The interval when staff have chosen none.</summary>
    public static ScheduleInterval Default { get; } = new(6);

    public int Hours { get; }

    /// <summary>The interval of <paramref name="hours"/>; any value but 1, 3, 6, 12 or 24 is refused.</summary>
    public static ScheduleInterval From(int hours) =>
        hours is 1 or 3 or 6 or 12 or 24
            ? new ScheduleInterval(hours)
            : throw new ArgumentOutOfRangeException(nameof(hours), hours, "The interval is 1, 3, 6, 12 or 24 hours.");
}
