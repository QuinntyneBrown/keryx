using System.Globalization;
using Keryx.Domain;

namespace Keryx.Application.AdminSettings;

/// <summary>
/// The settings as staff read and save them (L2-070), with the version they were read at. The
/// version is opaque to the client: it is sent back on a save so an older reading cannot
/// overwrite a newer change (L2-071 AC7).
/// </summary>
public sealed record SettingsDto(
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
    bool DailyDigest,
    string Version)
{
    internal static SettingsDto From(Settings settings) => new(
        settings.IntervalHours,
        settings.TimeoutSeconds,
        settings.PublishThreshold,
        [.. settings.Topics],
        [.. settings.Exclusions],
        [.. settings.FocusAreas],
        settings.IncludeOntarioWide,
        settings.IncludeNational,
        settings.MergeDuplicates,
        settings.RetentionMonths,
        settings.AlertEmail,
        settings.AlertFailureStreak,
        settings.AlertOnRunFailure,
        settings.DailyDigest,
        settings.Version.ToString(CultureInfo.InvariantCulture));
}
