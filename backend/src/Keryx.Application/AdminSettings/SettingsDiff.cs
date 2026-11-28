using Keryx.Domain;

namespace Keryx.Application.AdminSettings;

/// <summary>
/// The names of the settings a save changes, as the API writes them (L2-085 AC2). Only names: a
/// value, such as the alert address, is never returned.
/// </summary>
public static class SettingsDiff
{
    public static IReadOnlyList<string> ChangedFields(SettingsValues saved, SettingsValues submitted)
    {
        ArgumentNullException.ThrowIfNull(saved);
        ArgumentNullException.ThrowIfNull(submitted);
        var changed = new List<string>();
        Add(changed, "interval_hours", saved.IntervalHours != submitted.IntervalHours);
        Add(changed, "timeout_seconds", saved.TimeoutSeconds != submitted.TimeoutSeconds);
        Add(changed, "publish_threshold", saved.PublishThreshold != submitted.PublishThreshold);
        Add(changed, "topics", !saved.Topics.SequenceEqual(submitted.Topics, StringComparer.Ordinal));
        Add(changed, "exclusions", !saved.Exclusions.SequenceEqual(submitted.Exclusions, StringComparer.Ordinal));
        Add(changed, "focus_areas", !saved.FocusAreas.SequenceEqual(submitted.FocusAreas, StringComparer.Ordinal));
        Add(changed, "include_ontario_wide", saved.IncludeOntarioWide != submitted.IncludeOntarioWide);
        Add(changed, "include_national", saved.IncludeNational != submitted.IncludeNational);
        Add(changed, "merge_duplicates", saved.MergeDuplicates != submitted.MergeDuplicates);
        Add(changed, "retention_months", saved.RetentionMonths != submitted.RetentionMonths);
        Add(changed, "alert_email", !string.Equals(saved.AlertEmail, submitted.AlertEmail, StringComparison.Ordinal));
        Add(changed, "alert_failure_streak", saved.AlertFailureStreak != submitted.AlertFailureStreak);
        Add(changed, "alert_on_run_failure", saved.AlertOnRunFailure != submitted.AlertOnRunFailure);
        Add(changed, "daily_digest", saved.DailyDigest != submitted.DailyDigest);
        return changed;
    }

    private static void Add(List<string> changed, string name, bool differs)
    {
        if (differs)
        {
            changed.Add(name);
        }
    }
}
