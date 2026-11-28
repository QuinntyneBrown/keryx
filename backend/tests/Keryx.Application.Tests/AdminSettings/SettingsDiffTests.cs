using Keryx.Application.AdminSettings;
using Keryx.Domain;
using Keryx.Domain.Seeding;

namespace Keryx.Application.Tests.AdminSettings;

// Unit Test
// Traces to: L2-085 AC2
// Description: A save names each setting it changes, as the API writes it, and never its value.
// One case per row: each field changed alone, then none, then two.
public sealed class SettingsDiffTests
{
    private static readonly SettingsValues Saved = SeedData.DefaultValues() with { AlertEmail = "ops@keryx.example" };

    public static TheoryData<string> Fields { get; } =
    [
        "interval_hours", "timeout_seconds", "publish_threshold", "topics", "exclusions", "focus_areas",
        "include_ontario_wide", "include_national", "merge_duplicates", "retention_months", "alert_email",
        "alert_failure_streak", "alert_on_run_failure", "daily_digest",
    ];

    [Theory]
    [MemberData(nameof(Fields))]
    public void Given_one_field_changed_When_the_save_is_compared_Then_only_its_name_is_returned(string field) =>
        Assert.Equal([field], SettingsDiff.ChangedFields(Saved, Change(Saved, field)));

    [Fact]
    public void Given_nothing_changed_When_the_save_is_compared_Then_no_name_is_returned() =>
        Assert.Empty(SettingsDiff.ChangedFields(Saved, Saved with { Topics = [.. Saved.Topics] }));

    [Fact]
    public void Given_only_the_case_of_the_address_changed_When_the_save_is_compared_Then_it_is_a_change() =>
        Assert.Equal(["alert_email"], SettingsDiff.ChangedFields(Saved, Saved with { AlertEmail = "Ops@keryx.example" }));

    [Fact]
    public void Given_the_address_and_the_digest_changed_When_the_save_is_compared_Then_both_names_and_no_value_are_returned()
    {
        var changed = SettingsDiff.ChangedFields(Saved, Saved with { AlertEmail = "alerts@keryx.example", DailyDigest = !Saved.DailyDigest });

        Assert.Equal(["alert_email", "daily_digest"], changed);
        Assert.DoesNotContain(changed, name => name.Contains('@', StringComparison.Ordinal));
    }

    private static SettingsValues Change(SettingsValues values, string field) => field switch
    {
        "interval_hours" => values with { IntervalHours = values.IntervalHours + 1 },
        "timeout_seconds" => values with { TimeoutSeconds = values.TimeoutSeconds + 1 },
        "publish_threshold" => values with { PublishThreshold = values.PublishThreshold + 0.05m },
        "topics" => values with { Topics = [.. values.Topics, "rowing"] },
        "exclusions" => values with { Exclusions = [.. values.Exclusions, "casino"] },
        "focus_areas" => values with { FocusAreas = [.. values.FocusAreas, "Hamilton"] },
        "include_ontario_wide" => values with { IncludeOntarioWide = !values.IncludeOntarioWide },
        "include_national" => values with { IncludeNational = !values.IncludeNational },
        "merge_duplicates" => values with { MergeDuplicates = !values.MergeDuplicates },
        "retention_months" => values with { RetentionMonths = values.RetentionMonths + 1 },
        "alert_email" => values with { AlertEmail = "alerts@keryx.example" },
        "alert_failure_streak" => values with { AlertFailureStreak = values.AlertFailureStreak + 1 },
        "alert_on_run_failure" => values with { AlertOnRunFailure = !values.AlertOnRunFailure },
        "daily_digest" => values with { DailyDigest = !values.DailyDigest },
        _ => throw new ArgumentOutOfRangeException(nameof(field)),
    };
}
