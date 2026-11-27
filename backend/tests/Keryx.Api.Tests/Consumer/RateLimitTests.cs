using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using Microsoft.AspNetCore.Hosting;
using static Keryx.Api.Tests.Consumer.ConsumerApi;

namespace Keryx.Api.Tests.Consumer;

// Acceptance Test
// Traces to: L2-038 AC1-AC5
// Description: Each client has its own window of requests a minute; requests with no valid key
// are limited by address, before any key is looked up.
public sealed class RateLimitTests
{
    private static readonly Uri Articles = new("/v1/articles", UriKind.Relative);

    [Fact]
    public async Task Given_the_default_limit_of_600_When_a_key_sends_its_601st_request_in_a_minute_Then_it_is_429_rate_limited_with_Retry_After()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        using var client = Client(factory, await KeyAsync(factory, "articles:read"));
        for (var i = 1; i <= 600; i++)
        {
            using var allowed = await client.GetAsync(Articles, TestContext.Current.CancellationToken);
            Assert.True(allowed.StatusCode == HttpStatusCode.OK, $"request {i} was {(int)allowed.StatusCode}");
        }

        factory.Time.Advance(TimeSpan.FromSeconds(20));
        using var refused = await client.GetAsync(Articles, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(40), refused.Headers.RetryAfter?.Delta);
        var problem = await refused.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(("rate_limited", 429), (problem.GetProperty("code").GetString(), problem.GetProperty("status").GetInt32()));
    }

    [Fact]
    public async Task Given_a_limited_client_When_its_window_passes_Then_its_next_request_succeeds()
    {
        using var factory = new LimitedFactory(clientPermits: 2);
        await StaffClient.SeedAsync(factory);
        using var client = Client(factory, await KeyAsync(factory, "articles:read"));
        await SendAsync(client, 2);
        using (var refused = await client.GetAsync(Articles, TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        }

        factory.Time.Advance(TimeSpan.FromMinutes(1));
        using var next = await client.GetAsync(Articles, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
    }

    [Fact]
    public async Task Given_two_clients_When_one_is_limited_Then_the_other_is_unaffected()
    {
        using var factory = new LimitedFactory(clientPermits: 2);
        await StaffClient.SeedAsync(factory);
        using var first = Client(factory, await KeyAsync(factory, "articles:read"));
        using var second = Client(factory, await KeyAsync(factory, "articles:read"));
        await SendAsync(first, 2);

        using var refused = await first.GetAsync(Articles, TestContext.Current.CancellationToken);
        using var other = await second.GetAsync(Articles, TestContext.Current.CancellationToken);

        Assert.Equal((HttpStatusCode.TooManyRequests, HttpStatusCode.OK), (refused.StatusCode, other.StatusCode));
    }

    [Fact]
    public async Task Given_60_requests_with_no_valid_key_from_an_address_When_more_arrive_in_the_minute_Then_they_are_429_before_any_key_is_looked_up()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        var key = await KeyAsync(factory, "articles:read");
        using var anonymous = Client(factory, "kx_live_ABCDEFGHIJKLMNOPQRSTUVWX");
        for (var i = 1; i <= 60; i++)
        {
            using var unauthorized = await anonymous.GetAsync(Articles, TestContext.Current.CancellationToken);
            Assert.True(unauthorized.StatusCode == HttpStatusCode.Unauthorized, $"request {i} was {(int)unauthorized.StatusCode}");
        }

        using var refused = await anonymous.GetAsync(Articles, TestContext.Current.CancellationToken);
        // Even a valid key from that address is refused: its key is not looked up.
        using var valid = Client(factory, key);
        using var refusedToo = await valid.GetAsync(Articles, TestContext.Current.CancellationToken);

        Assert.Equal((HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests), (refused.StatusCode, refusedToo.StatusCode));
        Assert.Equal(TimeSpan.FromSeconds(60), refused.Headers.RetryAfter?.Delta);
        var problem = await refused.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("rate_limited", problem.GetProperty("code").GetString());
        factory.Time.Advance(TimeSpan.FromMinutes(1));
        using var later = await valid.GetAsync(Articles, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, later.StatusCode);
    }

    [Fact]
    public async Task Given_a_successful_response_When_it_is_sent_Then_it_carries_the_limit_what_remains_and_when_it_resets()
    {
        using var factory = new LimitedFactory(clientPermits: 5);
        await StaffClient.SeedAsync(factory);
        using var client = Client(factory, await KeyAsync(factory, "articles:read"));
        await SendAsync(client, 1);
        factory.Time.Advance(TimeSpan.FromSeconds(15));

        using var response = await client.GetAsync(Articles, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(("5", "3", "45"), (Header(response, "RateLimit-Limit"), Header(response, "RateLimit-Remaining"), Header(response, "RateLimit-Reset")));
    }

    private static string Header(HttpResponseMessage response, string name) => Assert.Single(response.Headers.GetValues(name));

    private static async Task SendAsync(HttpClient client, int count)
    {
        for (var i = 0; i < count; i++)
        {
            using var response = await client.GetAsync(Articles, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    /// <summary>A deployment that sets its own client limit (L2-038 AC1: configurable per deployment).</summary>
    private sealed class LimitedFactory(int clientPermits) : ApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Keryx:RateLimits:ClientPermitsPerMinute", clientPermits.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
