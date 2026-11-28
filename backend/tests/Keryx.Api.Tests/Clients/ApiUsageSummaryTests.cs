using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using Keryx.Application.Clients;
using Keryx.Domain.Clients;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

using static Keryx.Api.Tests.Consumer.ConsumerApi;

namespace Keryx.Api.Tests.Clients;

// Acceptance Test
// Traces to: L2-037 AC3; L2-067; L2-082
// Description: Staff read the whole API's usage over the last 24 hours: its public base address,
// how many requests every client made, their p95 latency read from the latency buckets, and the
// share that were errors. With no requests the p95 is null and the rate 0. Each test has its own
// store; the probe moves the fixture's clock to make a request take as long as asked.
public sealed class ApiUsageSummaryTests
{
    private static readonly Uri Usage = new("/admin/v1/api-usage", UriKind.Relative);
    private static readonly DateTimeOffset Now = new(2026, 11, 28, 14, 46, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_two_clients_calls_When_staff_read_usage_Then_the_totals_p95_and_error_rate_are_served()
    {
        using var factory = new ApiFactory();
        factory.Time.SetUtcNow(Now);
        await StaffClient.SeedAsync(factory);
        using var first = Client(factory, await KeyAsync(factory, ApiScopes.ArticlesRead));
        using var second = Client(factory, await KeyAsync(factory, ApiScopes.EventsRead));
        // Ten requests: one failure taking no time, eight of 40 ms, one of 300 ms. The 95th
        // percentile's rank is 9.5, half-way into the 250–500 ms bucket's one request: 375 ms.
        await GetAsync(first, "/v1/probe/throw");
        for (var call = 0; call < 8; call++)
        {
            await GetAsync(first, "/v1/probe/slow/40");
        }

        await GetAsync(second, "/v1/probe/slow/300");
        await FlushAsync(factory);
        using var staff = await StaffClient.OpenAsync(factory);

        using var response = await staff.GetAsync(Usage, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var usage = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(["base_url", "error_rate", "p95_latency_ms", "requests_24h"], usage.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(("https://api.keryx.test/v1", 10, 375, 0.1), (
            usage.GetProperty("base_url").GetString(),
            usage.GetProperty("requests_24h").GetInt32(),
            usage.GetProperty("p95_latency_ms").GetInt32(),
            usage.GetProperty("error_rate").GetDouble()));
    }

    [Fact]
    public async Task Given_no_requests_in_the_last_24_hours_When_staff_read_usage_Then_0_requests_no_p95_and_rate_0()
    {
        using var factory = new ApiFactory();
        factory.Time.SetUtcNow(Now);
        await StaffClient.SeedAsync(factory);
        using var product = Client(factory, await KeyAsync(factory, ApiScopes.ArticlesRead));
        await GetAsync(product, "/v1/probe/slow/40");
        await FlushAsync(factory);
        factory.Time.Advance(TimeSpan.FromHours(24));
        using var staff = await StaffClient.OpenAsync(factory);

        var usage = await staff.GetFromJsonAsync<JsonElement>(Usage, TestContext.Current.CancellationToken);

        Assert.Equal((0, JsonValueKind.Null, 0.0), (
            usage.GetProperty("requests_24h").GetInt32(),
            usage.GetProperty("p95_latency_ms").ValueKind,
            usage.GetProperty("error_rate").GetDouble()));
    }

    [Fact]
    public async Task Given_no_staff_session_When_usage_is_read_Then_401()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Usage, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task GetAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
    }

    private static async Task FlushAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new FlushApiUsage(), TestContext.Current.CancellationToken);
    }
}
