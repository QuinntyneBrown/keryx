using Keryx.Domain.Catalogue;
using Keryx.Domain.Collection;

namespace Keryx.Application.Tests.Catalogue;

/// <summary>
/// Who may undo a staff decision (L2-014 AC4). A session the API cannot name (one signed in before
/// sessions carried their id) never matches, so two such sessions cannot undo each other's decision.
/// </summary>
public sealed class ArticleDecisionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("s1", "s1", true)]
    [InlineData("s1", "s2", false)]
    [InlineData("", "", false)]
    public void Given_a_decision_in_one_session_When_a_session_reverts_it_Then_only_the_same_named_session_may(string decided, string reverting, bool reverted)
    {
        var article = Held();
        article.Publish(Now, decided);

        Assert.Equal(reverted, article.RevertTo(PublicationStatus.NeedsReview, reverting));
        Assert.Equal(reverted ? PublicationStatus.NeedsReview : PublicationStatus.Published, article.Status);
    }

    private static Article Held() => Article.Collect(
        new ArticleFacts("https://example.org/1", "Running and faith retreat", "All welcome.", "events", "global", [], null, 0.48m, PublicationStatus.NeedsReview) { Threshold = 0.60m },
        null, 1, "World Faith Fit", Now);
}
