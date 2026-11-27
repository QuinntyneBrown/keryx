using Keryx.Domain.Catalogue;

namespace Keryx.Application.Tests.Catalogue;

/// <summary>The rules that decide when two items are one record (L2-009 AC3, L2-010 AC2, AC5).</summary>
public sealed class MatchingRulesTests
{
    [Theory]
    [InlineData("https://Example.ORG/news/run?id=4#top", "https://example.org/news/run?id=4")]
    [InlineData("https://example.org/news/run?utm_source=x&id=4&utm_medium=y", "https://example.org/news/run?id=4")]
    [InlineData("https://example.org/news/run?fbclid=abc", "https://example.org/news/run")]
    [InlineData("https://example.org/news/run?gclid=abc&UTM_campaign=z", "https://example.org/news/run")]
    [InlineData("https://example.org:443/news/run/", "https://example.org/news/run")]
    [InlineData("http://example.org/News/Run?b=2&a=1", "http://example.org/News/Run?b=2&a=1")]
    public void Given_an_item_url_When_it_is_made_canonical_Then_tracking_and_the_fragment_are_gone(string url, string canonical) =>
        Assert.Equal(canonical, ItemUrl.Canonical(url));

    [Theory]
    [InlineData("")]
    [InlineData("/news/run")]
    [InlineData("ftp://example.org/run")]
    [InlineData("javascript:alert(1)")]
    public void Given_no_usable_url_When_it_is_made_canonical_Then_there_is_none(string url) => Assert.Null(ItemUrl.Canonical(url));

    // The shared case table of L2-010 AC2: the first three merge and the last does not.
    [Theory]
    [InlineData("Toronto churches host a spring 5K for refugees", "Toronto churches host a spring 5K for refugees", 1.00, true)]
    [InlineData("Toronto churches host a spring 5K for refugees", "  toronto CHURCHES host a spring-5K, for refugees!", 1.00, true)]
    [InlineData("One two three four five six seven eight nine ten", "One two three four five six seven eight nine eleven", 0.90, true)]
    [InlineData("One two three four five six seven eight nine ten", "One two three four five six seven eight twelve eleven", 0.80, false)]
    [InlineData("Toronto churches host a spring 5K for refugees", "New CrossFit gym opens in Scarborough", 0.00, false)]
    public void Given_two_headlines_When_they_are_compared_Then_they_merge_at_0_90_or_more(string first, string second, double score, bool merges)
    {
        Assert.Equal((decimal)score, HeadlineSimilarity.Score(first, second));
        Assert.Equal(merges, HeadlineSimilarity.Merges(first, second));
    }

    [Theory]
    [InlineData("The Redeemed Run Club", "redeemed run club")]
    [InlineData("Redeemed  Run-Club.", "redeemed run club")]
    [InlineData("REDEEMED RUN CLUB", "redeemed run club")]
    [InlineData("Theatre Fit", "theatre fit")]
    public void Given_an_organization_name_When_it_is_normalized_Then_case_punctuation_space_and_a_leading_The_are_ignored(string name, string normalized) =>
        Assert.Equal(normalized, OrganizationName.Normalize(name));
}

/// <summary>The kind an organization gets from the name it publishes under (L2-017).</summary>
public sealed class OrganizationKindTests
{
    [Theory]
    [InlineData("St. Paul's Anglican Church", "church")]
    [InlineData("Toronto Chinese Baptist Church", "church")]
    [InlineData("Athletes in Action Ministries", "ministry")]
    [InlineData("Faith Fitness Ministry", "ministry")]
    [InlineData("LifePoint Fitness", "gym")]
    [InlineData("Iron Sharpens Gym", "gym")]
    [InlineData("Redeemed Run Club", "club")]
    [InlineData("Christian Athletes Network", "network")]
    [InlineData("Grace Nutrition Inc.", "business")]
    [InlineData("World Faith Fit", "other")]
    public void Given_an_organization_name_When_its_kind_is_read_Then_the_first_matching_word_decides(string name, string kind) =>
        Assert.Equal(kind, OrganizationKinds.Word(OrganizationKinds.From(name)));
}
