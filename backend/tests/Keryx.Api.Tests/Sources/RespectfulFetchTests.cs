using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Fakes;
using Keryx.Api.Tests.Session;
using Keryx.Application.Sources;
using Keryx.Infrastructure.Sources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Keryx.Api.Tests.Sources;

// Acceptance Test
// Traces to: L2-002, L2-021, L2-022, L2-023, L2-024
// Description: The real source fetcher, behind the test-fetch endpoint, is a good and safe citizen.
public sealed class RespectfulFetchTests(RespectfulFetchTests.Factory factory) : IClassFixture<RespectfulFetchTests.Factory>
{
    private static readonly TimeSpan Within = TimeSpan.FromSeconds(10);

    private const string Feed = """
        <rss version="2.0"><channel>
          <item><title>Church fitness night</title><link>https://lifepointfit.ca/e/1</link><description>Toronto</description></item>
        </channel></rss>
        """;

    // L2-021: robots.txt is read first, and the client says who it is.
    [Fact]
    public async Task Given_robots_allows_the_path_When_a_feed_is_tested_Then_it_is_fetched_with_a_user_agent_naming_Keryx()
    {
        factory.Web.Text("https://allowed.example/robots.txt", "User-agent: *\nDisallow: /private/", "text/plain");
        factory.Web.Text("https://allowed.example/feed", Feed, "application/rss+xml");

        var body = await TestAsync("rss", "https://allowed.example/feed");

        Assert.True(body.GetProperty("reachable").GetBoolean());
        Assert.Equal(1, body.GetProperty("items_found").GetInt32());
        var fetch = Assert.Single(factory.Web.Requests, request => request.RequestUri!.AbsoluteUri == "https://allowed.example/feed");
        var agent = fetch.Headers.UserAgent.ToString();
        Assert.StartsWith("Keryx/", agent, StringComparison.Ordinal);
        Assert.Contains("+https://keryx.example/about-our-collector", agent, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("User-agent: *\nDisallow: /feed")]
    [InlineData("User-agent: Keryx\nDisallow: /\n\nUser-agent: *\nAllow: /")]
    public async Task Given_robots_disallows_the_path_When_it_is_tested_Then_it_is_blocked_by_robots_and_the_path_is_never_requested(string robots)
    {
        var host = $"blocked{Math.Abs(robots.GetHashCode(StringComparison.Ordinal))}.example";
        factory.Web.Text($"https://{host}/robots.txt", robots, "text/plain");
        factory.Web.Text($"https://{host}/feed", Feed, "application/rss+xml");

        var body = await TestAsync("rss", $"https://{host}/feed");

        Assert.Equal("blocked_by_robots", body.GetProperty("reason").GetString());
        Assert.DoesNotContain(factory.Web.Requests, request => request.RequestUri!.AbsoluteUri == $"https://{host}/feed");
    }

    [Fact]
    public async Task Given_a_404_When_it_is_tested_Then_it_is_not_found()
    {
        var body = await TestAsync("rss", "https://missing.example/feed");

        Assert.Equal("not_found", body.GetProperty("reason").GetString());
    }

    // L2-024: a redirect to a non-public host is refused before anything is sent to it.
    [Fact]
    public async Task Given_a_redirect_to_a_private_host_When_it_is_tested_Then_it_is_unsafe_address_and_that_host_is_never_requested()
    {
        factory.Hosts.Answer("internal.redirect.example", "10.20.30.40");
        factory.Web.On("https://redirect.example/feed", () => Redirect("https://internal.redirect.example/admin"));

        var body = await TestAsync("rss", "https://redirect.example/feed");

        Assert.Equal("unsafe_address", body.GetProperty("reason").GetString());
        Assert.DoesNotContain(factory.Web.Requests, request => request.RequestUri!.Host == "internal.redirect.example");
    }

    [Fact]
    public async Task Given_a_redirect_from_https_to_http_When_it_is_tested_Then_it_is_refused_and_not_followed()
    {
        factory.Web.On("https://downgrade.example/feed", () => Redirect("http://downgrade.example/feed"));

        var body = await TestAsync("rss", "https://downgrade.example/feed");

        Assert.Equal("refused", body.GetProperty("reason").GetString());
        Assert.DoesNotContain(factory.Web.Requests, request => request.RequestUri!.Scheme == "http");
    }

    [Fact]
    public async Task Given_a_chain_of_redirects_When_it_is_followed_Then_five_are_followed_and_a_sixth_is_refused()
    {
        for (var hop = 0; hop < 6; hop++)
        {
            var next = hop + 1;
            factory.Web.On($"https://chain.example/{hop}", () => Redirect($"https://chain.example/{next}"));
        }

        factory.Web.Text("https://chain.example/6", Feed, "application/rss+xml");
        factory.Web.Text("https://chain.example/5b", Feed, "application/rss+xml");
        factory.Web.On("https://short.example/0", () => Redirect("https://short.example/1"));
        factory.Web.Text("https://short.example/1", Feed, "application/rss+xml");

        var tooLong = await TestAsync("rss", "https://chain.example/0");
        var shortChain = await TestAsync("rss", "https://short.example/0");

        Assert.Equal("refused", tooLong.GetProperty("reason").GetString());
        Assert.DoesNotContain(factory.Web.Requests, request => request.RequestUri!.AbsoluteUri == "https://chain.example/6");
        Assert.True(shortChain.GetProperty("reachable").GetBoolean());
    }

    // L2-023: content of a type the kind does not expect, or too large, is not parsed.
    [Fact]
    public async Task Given_content_of_the_wrong_type_When_it_is_tested_Then_it_is_invalid_content()
    {
        factory.Web.Text("https://image.example/feed", Feed, "image/png");

        var body = await TestAsync("rss", "https://image.example/feed");

        Assert.Equal("invalid_content", body.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Given_content_over_5_MB_When_it_is_tested_Then_it_is_invalid_content()
    {
        var huge = "<rss version=\"2.0\"><channel>" + new string(' ', 5 * 1024 * 1024) + "</channel></rss>";
        factory.Web.Text("https://huge.example/feed", huge, "application/rss+xml");

        var body = await TestAsync("rss", "https://huge.example/feed");

        Assert.Equal("invalid_content", body.GetProperty("reason").GetString());
    }

    // L2-022: every attempt has a timeout; three attempts at most.
    [Fact]
    public async Task Given_a_source_that_never_answers_When_it_is_tested_Then_three_attempts_time_out_and_the_reason_is_timeout()
    {
        factory.Web.On("https://slow.example/feed", async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        await StaffClient.SeedAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);

        var response = client.PostAsJsonAsync(new Uri("/admin/v1/sources/test", UriKind.Relative), new { kind = "rss", address = "https://slow.example/feed" }, TestContext.Current.CancellationToken);
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            await factory.Web.NextAsync("https://slow.example/feed", Within);
            await factory.Time.TimerAsync(TimeSpan.FromSeconds(30), Within);
            factory.Time.Advance(TimeSpan.FromSeconds(30));
            if (attempt < 3)
            {
                // The back-off before the next attempt: 1 second, then 2.
                var backOff = TimeSpan.FromSeconds(attempt);
                await factory.Time.TimerAsync(backOff, Within);
                factory.Time.Advance(backOff);
            }
        }

        using var answered = await response.WaitAsync(Within, TestContext.Current.CancellationToken);
        var body = await answered.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("timeout", body.GetProperty("reason").GetString());
        Assert.Equal(3, factory.Web.Requests.Count(request => request.RequestUri!.AbsoluteUri == "https://slow.example/feed"));
    }

    // L2-022: after 429 with Retry-After, the next attempt waits at least that long.
    [Fact]
    public async Task Given_429_with_retry_after_When_it_is_tested_Then_it_waits_that_long_and_tries_again()
    {
        var calls = 0;
        factory.Web.On("https://busy.example/feed", () => Interlocked.Increment(ref calls) == 1
            ? new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Headers = { RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(7)) } }
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Feed, System.Text.Encoding.UTF8, "application/rss+xml") });
        await StaffClient.SeedAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);

        var response = client.PostAsJsonAsync(new Uri("/admin/v1/sources/test", UriKind.Relative), new { kind = "rss", address = "https://busy.example/feed" }, TestContext.Current.CancellationToken);
        await factory.Web.NextAsync("https://busy.example/feed", Within);
        var waited = await factory.Time.TimerAsync(TimeSpan.FromSeconds(7), Within);
        factory.Time.Advance(waited);

        using var answered = await response.WaitAsync(Within, TestContext.Current.CancellationToken);
        var body = await answered.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(TimeSpan.FromSeconds(7), waited);
        Assert.True(body.GetProperty("reachable").GetBoolean());
        Assert.Equal(2, calls);
    }

    private static HttpResponseMessage Redirect(string location) =>
        new(HttpStatusCode.Found) { Headers = { Location = new Uri(location) } };

    private async Task<JsonElement> TestAsync(string kind, string address)
    {
        await StaffClient.SeedAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);
        using var response = await client.PostAsJsonAsync(new Uri("/admin/v1/sources/test", UriKind.Relative), new { kind, address }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    /// <summary>The API with the real source fetcher, whose sockets are replaced by <see cref="FakeWeb"/>.</summary>
    public sealed class Factory : ApiFactory
    {
        public FakeWeb Web { get; } = new();

        protected override void ConfigureServices(IServiceCollection services)
        {
            services.RemoveAll<ISourceFetcher>();
            services.AddSingleton<ISourceFetcher, HttpSourceFetcher>();
            services.RemoveAll<SourceSockets>();
            services.AddSingleton(new SourceSockets(Web));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Web.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
