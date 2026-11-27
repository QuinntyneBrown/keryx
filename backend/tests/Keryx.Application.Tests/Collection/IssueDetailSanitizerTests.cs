using Keryx.Application.Collection;

namespace Keryx.Application.Tests.Collection;

/// <summary>An issue's detail holds no query string, credential, internal host or IP address, and is bounded (L2-020 AC5).</summary>
public sealed class IssueDetailSanitizerTests
{
    [Theory]
    [InlineData("Nothing was found at https://redeemedrun.ca/feed.", "Nothing was found at https://redeemedrun.ca/feed.")]
    [InlineData("Nothing was found at https://redeemedrun.ca/feed?utm_source=x&page=2.", "Nothing was found at https://redeemedrun.ca/feed.")]
    [InlineData("Nothing was found at https://redeemedrun.ca/feed?x=1#top.", "Nothing was found at https://redeemedrun.ca/feed.")]
    [InlineData("Refused at https://admin:hunter2@redeemedrun.ca/feed.", "Refused at https://redeemedrun.ca/feed.")]
    [InlineData("Failed with api_key=sk_live_abc123 and token: eyJhbGciOi.", "Failed with api_key=[removed] and token: [removed].")]
    [InlineData("Failed with Authorization: Bearer eyJhbGciOiJIUzI1NiJ9.", "Failed with Authorization: Bearer [removed].")]
    [InlineData("connect ECONNREFUSED 10.0.4.12:5432 (3 retries)", "connect ECONNREFUSED [address] (3 retries)")]
    [InlineData("Resolved to 192.168.1.20 and fe80::1ff:fe23:4567:890a.", "Resolved to [address] and [address].")]
    [InlineData("Could not reach db.internal or http://localhost:8080/health or cache.local.", "Could not reach [host] or http://[host]/health or [host].")]
    [InlineData("Version 1.2.3 is fine.", "Version 1.2.3 is fine.")]
    public void Given_a_detail_When_it_is_sanitized_Then_nothing_internal_or_secret_is_left(string detail, string expected) =>
        Assert.Equal(expected, IssueDetailSanitizer.Sanitize(detail));

    [Fact]
    public void Given_a_long_detail_When_it_is_sanitized_Then_it_is_at_most_300_characters_ending_with_an_ellipsis()
    {
        var sanitized = IssueDetailSanitizer.Sanitize(new string('a', 400));

        Assert.Equal(300, sanitized.Length);
        Assert.EndsWith("…", sanitized, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_a_detail_of_exactly_300_characters_When_it_is_sanitized_Then_it_is_kept_whole() =>
        Assert.Equal(new string('a', 300), IssueDetailSanitizer.Sanitize(new string('a', 300)));
}
