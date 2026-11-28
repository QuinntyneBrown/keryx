namespace Keryx.Domain;

/// <summary>The values staff control (L2-070), as one change sends them.</summary>
public sealed record SettingsValues(
    int IntervalHours,
    int TimeoutSeconds,
    decimal PublishThreshold,
    IReadOnlyList<string> Topics,
    IReadOnlyList<string> Exclusions,
    IReadOnlyList<string> FocusAreas,
    bool IncludeOntarioWide,
    bool IncludeNational,
    bool MergeDuplicates,
    int RetentionMonths,
    string? AlertEmail,
    int AlertFailureStreak,
    bool AlertOnRunFailure,
    bool DailyDigest);

/// <summary>
/// What staff control about collection (L2-070): the schedule, what is relevant, where, how long
/// records are kept and who is told of trouble. There is exactly one row, <see cref="SingletonId"/>;
/// seed data creates it with the defaults and staff change it afterwards. Each change raises its
/// <see cref="Version"/>, so a change made from an older reading is refused (L2-071 AC7).
/// </summary>
public sealed class Settings
{
    /// <summary>The id of the one settings row.</summary>
    public const int SingletonId = 1;

    private Settings()
    {
    }

    public int Id { get; private set; } = SingletonId;

    /// <summary>Raised by every change; a save names the version it was made from.</summary>
    public int Version { get; private set; }

    /// <summary>Hours between scheduled collections: 1, 3, 6, 12 or 24.</summary>
    public int IntervalHours { get; private set; }

    /// <summary>Seconds a source may take to answer.</summary>
    public int TimeoutSeconds { get; private set; }

    /// <summary>The relevance score at or above which an item is published without review.</summary>
    public decimal PublishThreshold { get; private set; }

    /// <summary>Words that make an item relevant.</summary>
    public IList<string> Topics { get; private set; } = [];

    /// <summary>Words that make an item irrelevant.</summary>
    public IList<string> Exclusions { get; private set; } = [];

    /// <summary>The places collection concentrates on, by name.</summary>
    public IList<string> FocusAreas { get; private set; } = [];

    public bool IncludeOntarioWide { get; private set; }

    public bool IncludeNational { get; private set; }

    /// <summary>The same story from two sources is one record.</summary>
    public bool MergeDuplicates { get; private set; }

    /// <summary>Months a record is kept before retention removes it.</summary>
    public int RetentionMonths { get; private set; }

    /// <summary>Where alerts go. With none, none is sent.</summary>
    public string? AlertEmail { get; private set; }

    /// <summary>Consecutive failed runs of a source before an alert.</summary>
    public int AlertFailureStreak { get; private set; }

    public bool AlertOnRunFailure { get; private set; }

    public bool DailyDigest { get; private set; }

    /// <summary>The settings a deployment starts with.</summary>
    public static Settings Create(SettingsValues values)
    {
        var settings = new Settings();
        settings.Apply(values);
        return settings;
    }

    /// <summary>The values as staff last saved them.</summary>
    public SettingsValues Values() => new(
        IntervalHours, TimeoutSeconds, PublishThreshold, [.. Topics], [.. Exclusions], [.. FocusAreas], IncludeOntarioWide,
        IncludeNational, MergeDuplicates, RetentionMonths, AlertEmail, AlertFailureStreak, AlertOnRunFailure, DailyDigest);

    /// <summary>Takes staff's values and raises the version.</summary>
    public void Update(SettingsValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        Apply(values);
        Version++;
    }

    private void Apply(SettingsValues values)
    {
        IntervalHours = values.IntervalHours;
        TimeoutSeconds = values.TimeoutSeconds;
        PublishThreshold = values.PublishThreshold;
        Topics = [.. values.Topics];
        Exclusions = [.. values.Exclusions];
        FocusAreas = [.. values.FocusAreas];
        IncludeOntarioWide = values.IncludeOntarioWide;
        IncludeNational = values.IncludeNational;
        MergeDuplicates = values.MergeDuplicates;
        RetentionMonths = values.RetentionMonths;
        AlertEmail = values.AlertEmail;
        AlertFailureStreak = values.AlertFailureStreak;
        AlertOnRunFailure = values.AlertOnRunFailure;
        DailyDigest = values.DailyDigest;
    }
}
