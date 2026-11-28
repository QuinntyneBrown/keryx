using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using Keryx.Application.Clients;
using Keryx.Domain.Clients;
using Keryx.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using static Keryx.Api.Tests.Consumer.ConsumerApi;

namespace Keryx.Api.Tests.Clients;

// Acceptance Test
// Traces to: L2-037 AC1, AC2, AC4; L2-067
// Description: Each client's requests through the consumer API are counted per hour once the
// counts are flushed: every response is a request and a 5xx is also an error. The clients list
// serves each client's requests, errors and error rate over the current hour and the 23 before
// it, and its last call. A request with no valid key is not counted. Each test has its own store.
public sealed class ApiUsageTests
{
    /// <summary>Saturday 28 November 2026, 9:46 AM in Toronto.</summary>
    private static readonly DateTimeOffset Now = new(2026, 11, 28, 14, 46, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_a_client_that_called_When_staff_list_clients_Then_its_requests_errors_rate_and_last_call_are_served()
    {
        using var factory = new ApiFactory();
        factory.Time.SetUtcNow(Now);
        await StaffClient.SeedAsync(factory);
        var key = await KeyAsync(factory, ApiScopes.ArticlesRead);
        await KeyAsync(factory, ApiScopes.EventsRead);
        using var product = Client(factory, key);
        using var stranger = Client(factory, "kx_live_notakey0000000000000000000000");

        Assert.Equal(HttpStatusCode.OK, await StatusAsync(product, "/v1/articles"));
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(product, "/v1/articles/art_missing"));
        Assert.Equal(HttpStatusCode.InternalServerError, await StatusAsync(product, "/v1/probe/throw"));
        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(stranger, "/v1/articles"));
        await FlushAsync(factory);
        // Counts added after a flush join the same hour's counts.
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(product, "/v1/articles"));
        await FlushAsync(factory);

        var (called, idle) = await ClientsAsync(factory);
        Assert.Equal((4, 1, 0.25, (DateTimeOffset?)Now), Usage(called));
        Assert.Equal((0, 0, 0.0, (DateTimeOffset?)null), Usage(idle));
        await using var scope = factory.Services.CreateAsyncScope();
        var hour = Assert.Single(await scope.ServiceProvider.GetRequiredService<KeryxDbContext>().ApiUsageHours.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal((new DateTimeOffset(2026, 11, 28, 14, 0, 0, TimeSpan.Zero), 4, 1), (hour.HourStart, hour.Requests, hour.Errors));
    }

    [Theory]
    [InlineData(23 * 60 + 13, 1)] // next day 13:59 UTC: the 14:00 hour is the oldest of the 24
    [InlineData(23 * 60 + 14, 0)] // next day 14:00 UTC: the window starts at 15:00
    public async Task Given_a_call_When_the_clock_moves_on_Then_it_counts_while_its_hour_is_one_of_the_last_24(int minutes, int requests)
    {
        using var factory = new ApiFactory();
        factory.Time.SetUtcNow(Now);
        await StaffClient.SeedAsync(factory);
        var key = await KeyAsync(factory, ApiScopes.ArticlesRead);
        using var product = Client(factory, key);
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(product, "/v1/articles"));
        await FlushAsync(factory);

        factory.Time.Advance(TimeSpan.FromMinutes(minutes));

        var (called, _) = await ClientsAsync(factory, single: true);
        Assert.Equal((requests, Now), (called.GetProperty("requests_24h").GetInt32(), called.GetProperty("last_call_at").GetDateTimeOffset()));
    }

    [Fact]
    public async Task Given_calls_not_yet_flushed_When_staff_list_clients_Then_they_are_not_counted_yet()
    {
        using var factory = new ApiFactory();
        factory.Time.SetUtcNow(Now);
        await StaffClient.SeedAsync(factory);
        var key = await KeyAsync(factory, ApiScopes.ArticlesRead);
        using var product = Client(factory, key);

        Assert.Equal(HttpStatusCode.OK, await StatusAsync(product, "/v1/articles"));

        var (called, _) = await ClientsAsync(factory, single: true);
        Assert.Equal((0, (DateTimeOffset?)null), (called.GetProperty("requests_24h").GetInt32(), LastCall(called)));
    }

    private static async Task<HttpStatusCode> StatusAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
        return response.StatusCode;
    }

    private static async Task FlushAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new FlushApiUsage(), TestContext.Current.CancellationToken);
    }

    /// <summary>The clients, newest first: the one that called was created first, so it is listed second.</summary>
    private static async Task<(JsonElement Called, JsonElement Idle)> ClientsAsync(ApiFactory factory, bool single = false)
    {
        using var staff = await StaffClient.OpenAsync(factory);
        var listed = await staff.GetFromJsonAsync<JsonElement>(new Uri("/admin/v1/api-clients", UriKind.Relative), TestContext.Current.CancellationToken);
        var items = listed.GetProperty("items").EnumerateArray().ToList();
        return single ? (Assert.Single(items), default) : (items[1], items[0]);
    }

    private static (int Requests, int Errors, double Rate, DateTimeOffset? LastCall) Usage(JsonElement client) => (
        client.GetProperty("requests_24h").GetInt32(),
        client.GetProperty("errors_24h").GetInt32(),
        client.GetProperty("error_rate").GetDouble(),
        LastCall(client));

    private static DateTimeOffset? LastCall(JsonElement client) =>
        client.GetProperty("last_call_at").ValueKind == JsonValueKind.Null ? null : client.GetProperty("last_call_at").GetDateTimeOffset();
}
