using Keryx.Domain.Collection;

namespace Keryx.Application.Tests.Collection;

/// <summary>
/// Publication status from thresholds (L2-013). The same inline case table is in the frontend's
/// publicationStatusFor tests (score-and-publish design).
/// </summary>
public sealed class PublicationPolicyTests
{
    [Theory]
    [InlineData("0.60", "1.00", PublicationStatus.Published)]
    [InlineData("0.60", "0.60", PublicationStatus.Published)]
    [InlineData("0.60", "0.59", PublicationStatus.NeedsReview)]
    [InlineData("0.60", "0.45", PublicationStatus.NeedsReview)]
    [InlineData("0.60", "0.44", PublicationStatus.Hidden)]
    [InlineData("0.60", "0.00", PublicationStatus.Hidden)]
    [InlineData("0.40", "0.44", PublicationStatus.Published)]
    [InlineData("0.40", "0.40", PublicationStatus.Published)]
    [InlineData("0.40", "0.39", PublicationStatus.Hidden)]
    public void Given_a_threshold_and_a_score_When_classified_Then_the_status_follows(string threshold, string score, PublicationStatus status) =>
        Assert.Equal(status, PublicationPolicy.Classify(decimal.Parse(score, System.Globalization.CultureInfo.InvariantCulture), decimal.Parse(threshold, System.Globalization.CultureInfo.InvariantCulture)));
}
