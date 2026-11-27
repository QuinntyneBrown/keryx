using Keryx.Application.Scoring;
using Keryx.Domain.Seeding;

namespace Keryx.Application.Tests.Sources;

// L2-012: the relevance score of an item, from the topics, exclusions and geography in Settings.
public sealed class RelevanceScorerTests
{
    private static readonly ScoringSettings Defaults = ScoringSettings.From(SeedData.DefaultSettings());
    private static readonly RelevanceScorer Scorer = new();

    // AC1: the scoring case table, in descending order, with the defaults.
    [Fact]
    public void Given_the_case_table_When_each_item_is_scored_Then_the_scores_descend_and_the_last_two_are_below_the_review_floor()
    {
        var inFocus = Scorer.Score(new ScoringItem("Church fitness night", "Running and strength for the whole family.", "Mississauga, ON"), Defaults);
        var outsideFocus = Scorer.Score(new ScoringItem("Church fitness night", "Running and strength for the whole family.", "Vancouver, BC"), Defaults);
        var excluded = Scorer.Score(new ScoringItem("Church fitness casino night", "Running and strength.", "Toronto, ON"), Defaults);
        var noTopic = Scorer.Score(new ScoringItem("City council meeting", "Budget and zoning.", "Toronto, ON"), Defaults);

        Assert.True(inFocus.Value > outsideFocus.Value, $"{inFocus} > {outsideFocus}");
        Assert.True(outsideFocus.Value > excluded.Value, $"{outsideFocus} > {excluded}");
        Assert.True(outsideFocus.Value > noTopic.Value, $"{outsideFocus} > {noTopic}");
        Assert.True(excluded.Value < 0.45m, $"{excluded}");
        Assert.True(noTopic.Value < 0.45m, $"{noTopic}");
    }

    // The weights, one row per case, worked from the rule: 0.6 × topic share + 0.4 × geography.
    [Theory]
    [InlineData("Church fitness and running", "Strength classes", "Toronto, ON", 1.00)] // 3 topics, focus
    [InlineData("Church fitness", "", "Toronto, ON", 0.80)] // 2 topics, focus
    [InlineData("Fitness", "", "Toronto, ON", 0.60)] // 1 topic, focus
    [InlineData("Church fitness", "", "Hamilton, Ontario", 0.64)] // Ontario-wide on
    [InlineData("Church fitness", "", "Vancouver, BC", 0.48)] // outside
    [InlineData("Church fitness", "", "", 0.48)] // no place
    [InlineData("Sponsored fitness", "", "Toronto, ON", 0.10)] // exclusion
    [InlineData("Weather", "", "Toronto, ON", 0.30)] // no topic, focus
    [InlineData("Weather", "", "Vancouver, BC", 0.06)] // no topic, outside
    public void Given_an_item_When_it_is_scored_with_the_defaults_Then_it_has_the_worked_score(string title, string summary, string place, double expected) =>
        Assert.Equal((decimal)expected, Scorer.Score(new ScoringItem(title, summary, place), Defaults).Value);

    // AC2.
    [Fact]
    public void Given_the_same_item_and_settings_When_scored_twice_Then_the_scores_are_equal()
    {
        var item = new ScoringItem("Sports ministry run club", "Wellness and faith.", "Ajax, ON");

        Assert.Equal(Scorer.Score(item, Defaults), Scorer.Score(item, Defaults));
    }

    // AC3.
    [Fact]
    public void Given_an_Ontario_item_outside_the_focus_areas_When_Ontario_wide_is_on_Then_it_scores_higher_than_with_it_off()
    {
        var item = new ScoringItem("Church fitness", "", "Hamilton, Ontario");
        var off = Defaults with { IncludeOntarioWide = false };

        Assert.True(Scorer.Score(item, Defaults).Value > Scorer.Score(item, off).Value);
    }

    // AC4: two decimal places, half away from zero, between 0 and 1.
    [Theory]
    [InlineData(0.585, 0.59)]
    [InlineData(0.584, 0.58)]
    [InlineData(0.005, 0.01)]
    [InlineData(1.2, 1.00)]
    [InlineData(-0.1, 0.00)]
    public void Given_a_raw_score_When_it_becomes_a_relevance_score_Then_it_is_rounded_and_clamped(double raw, double served) =>
        Assert.Equal((decimal)served, RelevanceScore.Of((decimal)raw).Value);

    [Fact]
    public void Given_a_topic_inside_a_longer_word_When_scored_Then_it_does_not_match()
    {
        // "faithful" is not "faith", "churches" is not "church": whole words only.
        Assert.Equal(0.30m, Scorer.Score(new ScoringItem("Faithful churches", "", "Toronto, ON"), Defaults).Value);
    }
}
