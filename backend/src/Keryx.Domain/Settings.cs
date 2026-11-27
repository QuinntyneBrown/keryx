namespace Keryx.Domain;

/// <summary>
/// What staff control about collection (L2-070): the schedule, what is relevant, where, how long
/// records are kept and who is told of trouble. There is exactly one row, <see cref="SingletonId"/>;
/// seed data creates it with the defaults and staff change it afterwards.
/// </summary>
public sealed class Settings
{
    /// <summary>The id of the one settings row.</summary>
    public const int SingletonId = 1;

    public int Id { get; private set; } = SingletonId;

    /// <summary>Hours between scheduled collections: 1, 3, 6, 12 or 24.</summary>
    public int IntervalHours { get; init; }

    /// <summary>Seconds a source may take to answer.</summary>
    public int TimeoutSeconds { get; init; }

    /// <summary>The relevance score at or above which an item is published without review.</summary>
    public decimal PublishThreshold { get; init; }

    /// <summary>Words that make an item relevant.</summary>
    public IList<string> Topics { get; init; } = [];

    /// <summary>Words that make an item irrelevant.</summary>
    public IList<string> Exclusions { get; init; } = [];

    /// <summary>The places collection concentrates on, by name.</summary>
    public IList<string> FocusAreas { get; init; } = [];

    public bool IncludeOntarioWide { get; init; }

    public bool IncludeNational { get; init; }

    /// <summary>The same story from two sources is one record.</summary>
    public bool MergeDuplicates { get; init; }

    /// <summary>Months a record is kept before retention removes it.</summary>
    public int RetentionMonths { get; init; }

    /// <summary>Where alerts go. With none, none is sent.</summary>
    public string? AlertEmail { get; init; }

    /// <summary>Consecutive failed runs of a source before an alert.</summary>
    public int AlertFailureStreak { get; init; }

    public bool AlertOnRunFailure { get; init; }

    public bool DailyDigest { get; init; }
}
