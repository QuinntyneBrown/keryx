using Keryx.Domain.Catalogue;

namespace Keryx.Application.Tests.Catalogue;

/// <summary>The bounds of an article's fields (L2-015) and the removal of contact details (L2-081).</summary>
public sealed class ArticleFieldsTests
{
    [Fact]
    public void Given_a_title_of_300_characters_When_it_is_bounded_Then_it_is_kept_whole()
    {
        var title = new string('t', 300);

        Assert.Equal(title, ArticleTitle.From(title));
    }

    [Fact]
    public void Given_a_title_longer_than_300_characters_When_it_is_bounded_Then_it_ends_on_a_word_with_an_ellipsis_within_300()
    {
        var title = string.Join(' ', Enumerable.Repeat("strength", 40));

        var bounded = ArticleTitle.From(title)!;

        Assert.True(bounded.Length <= 300);
        Assert.EndsWith("strength…", bounded, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(301)]
    [InlineData(302)]
    public void Given_a_title_just_past_300_characters_When_it_is_bounded_Then_it_is_within_300(int length) =>
        Assert.True(ArticleTitle.From("Run " + new string('x', length - 4))!.Length <= ArticleTitle.MaxLength);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_no_title_When_it_is_bounded_Then_it_is_null(string? title) => Assert.Null(ArticleTitle.From(title));

    [Fact]
    public void Given_a_long_summary_When_it_is_bounded_Then_it_is_at_most_500_characters_ending_on_a_word()
    {
        var summary = string.Join(' ', Enumerable.Repeat("Bootcamp", 700));

        var bounded = ArticleSummary.From(summary, null);

        Assert.True(bounded.Length <= 500);
        Assert.EndsWith("Bootcamp…", bounded, StringComparison.Ordinal);
    }

    [Theory]
    // L2-015 AC2: with no summary, the item's first sentence.
    [InlineData("", "Join us at dawn. Bring water and a friend.", "Join us at dawn.")]
    [InlineData(null, "Sunrise run! Coffee after.", "Sunrise run!")]
    [InlineData(null, "No full stop here", "No full stop here")]
    [InlineData("A summary.", "A body. More body.", "A summary.")]
    [InlineData(null, null, "")]
    public void Given_a_summary_or_none_When_it_is_bounded_Then_it_is_the_summary_or_the_first_sentence(string? summary, string? text, string expected) =>
        Assert.Equal(expected, ArticleSummary.From(summary, text));

    [Theory]
    // L2-081 AC3: e-mail addresses and phone numbers are removed.
    [InlineData("Write to pastor.dan@gmail.com for details.", "Write to for details.")]
    [InlineData("Call 416-555-0199 or (647) 555 0123 today.", "Call or today.")]
    [InlineData("Call +1 416 555 0199.", "Call.")]
    [InlineData("Doors open at 6:30, 5 km route, since 2019.", "Doors open at 6:30, 5 km route, since 2019.")]
    public void Given_text_with_contact_details_When_it_is_scrubbed_Then_none_is_left(string text, string expected) =>
        Assert.Equal(expected, ContactDetailScrubber.Scrub(text));

    [Fact]
    public void Given_tags_When_they_are_bounded_Then_they_are_lower_cased_de_duplicated_at_most_30_and_none_longer_than_40()
    {
        var tags = new[] { "Running", "running", " Faith ", new string('x', 41), "" }
            .Concat(Enumerable.Range(1, 40).Select(i => $"tag{i}"));

        var bounded = TagSet.From(tags);

        Assert.Equal(30, bounded.Count);
        Assert.Equal(["running", "faith", "tag1"], bounded.Take(3));
        Assert.DoesNotContain(bounded, tag => tag.Length > 40);
    }

    [Theory]
    // L2-015 AC5: exactly one of the six, by the source's own tags first, then the words of the item.
    [InlineData("Weekly fellowship", new[] { "Nutrition" }, "nutrition")]
    [InlineData("Toronto Waterfront 10K raises funds", new string[0], "races-challenges")]
    [InlineData("Spring prayer and fitness camp", new string[0], "events")]
    [InlineData("New CrossFit gym opens in Scarborough", new string[0], "gyms-studios")]
    [InlineData("Healthy meal plans for families", new string[0], "nutrition")]
    [InlineData("Sports ministry launches youth outreach", new string[0], "ministry-programs")]
    [InlineData("Rest, prayer and the body", new string[0], "faith-wellness")]
    public void Given_an_item_When_it_is_categorized_Then_it_gets_exactly_one_category(string title, string[] tags, string category) =>
        Assert.Equal(category, ArticleCategory.For(title, string.Empty, tags));
}
