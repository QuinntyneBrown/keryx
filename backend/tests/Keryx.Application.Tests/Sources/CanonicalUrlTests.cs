using Keryx.Domain.Sources;

namespace Keryx.Application.Tests.Sources;

// L2-001 AC4: addresses that differ only by scheme case, host case, a trailing slash or a
// fragment are the same source; anything else is a different one.
public sealed class CanonicalUrlTests
{
    [Theory]
    [InlineData("https://lifepointfit.ca/events/feed", "https://lifepointfit.ca/events/feed")]
    [InlineData("HTTPS://lifepointfit.ca/events/feed", "https://lifepointfit.ca/events/feed")]
    [InlineData("https://LifePointFit.CA/events/feed", "https://lifepointfit.ca/events/feed")]
    [InlineData("https://lifepointfit.ca/events/feed/", "https://lifepointfit.ca/events/feed")]
    [InlineData("https://lifepointfit.ca/events/feed#latest", "https://lifepointfit.ca/events/feed")]
    [InlineData("HTTPS://LifePointFit.CA/events/feed/#top", "https://lifepointfit.ca/events/feed")]
    [InlineData("https://lifepointfit.ca/", "https://lifepointfit.ca")]
    [InlineData("https://lifepointfit.ca", "https://lifepointfit.ca")]
    [InlineData("https://lifepointfit.ca:443/events", "https://lifepointfit.ca/events")]
    [InlineData("http://lifepointfit.ca:80/events", "http://lifepointfit.ca/events")]
    [InlineData("https://lifepointfit.ca/events?page=2", "https://lifepointfit.ca/events?page=2")]
    public void Given_an_address_When_it_is_normalized_Then_it_is_the_canonical_form(string address, string canonical) =>
        Assert.Equal(canonical, CanonicalUrl.Normalize(address));

    [Theory]
    [InlineData("https://lifepointfit.ca/Events", "https://lifepointfit.ca/events")]
    [InlineData("http://lifepointfit.ca/events", "https://lifepointfit.ca/events")]
    [InlineData("https://lifepointfit.ca/events?page=2", "https://lifepointfit.ca/events?page=3")]
    [InlineData("https://www.lifepointfit.ca/events", "https://lifepointfit.ca/events")]
    public void Given_addresses_that_differ_otherwise_When_they_are_normalized_Then_they_stay_different(string first, string second) =>
        Assert.NotEqual(CanonicalUrl.Normalize(first), CanonicalUrl.Normalize(second));
}
