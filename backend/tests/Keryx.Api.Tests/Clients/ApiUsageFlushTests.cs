using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Fakes;
using Keryx.Api.Tests.Session;
using Keryx.Application.Abstractions;
using Keryx.Domain.Clients;
using Keryx.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

using static Keryx.Api.Tests.Consumer.ConsumerApi;

namespace Keryx.Api.Tests.Clients;

// Acceptance Test
// Traces to: L2-037 AC1
// Description: The API flushes the usage it counted every 60 seconds by its clock, and once more
// as it stops. A flush the database refuses loses nothing: its counts join the next flush, and the
// timer goes on. It never sleeps: it waits until the service waits on the clock, then advances it.
public sealed class ApiUsageFlushTests
{
    private static readonly TimeSpan Minute = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan Within = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Given_a_call_When_the_clock_moves_60_seconds_Then_it_is_counted_and_the_timer_goes_on()
    {
        using var factory = new FlushFactory();
        await ArrangeAsync(factory);
        await factory.Time.TimerAsync(Minute, Within);
        await CallAsync(factory);
        Assert.Equal(0, await RequestsAsync(factory));

        factory.Time.Advance(Minute);
        await factory.Time.TimerAsync(Minute, Within);

        Assert.Equal(1, await RequestsAsync(factory));
    }

    [Fact]
    public async Task Given_a_call_When_the_API_stops_Then_it_is_counted()
    {
        var factory = new FlushFactory();
        await ArrangeAsync(factory);
        await CallAsync(factory);

        // Disposing the fixture stops its host, as a deployment stops the API.
        await factory.DisposeAsync();

        await using var store = TestStore.Open(factory.Store);
        var hour = Assert.Single(store.ApiUsageHours);
        Assert.Equal(1, hour.Requests);
    }

    [Fact]
    public async Task Given_a_flush_the_database_refuses_When_the_next_one_runs_Then_both_minutes_calls_are_counted()
    {
        using var factory = new FlushFactory();
        await ArrangeAsync(factory);
        await factory.Time.TimerAsync(Minute, Within);
        await CallAsync(factory);
        factory.Fault.UsageArmed = true;

        factory.Time.Advance(Minute);
        await factory.Time.TimerAsync(Minute, Within);
        Assert.Equal(0, await RequestsAsync(factory));
        factory.Fault.UsageArmed = false;
        await CallAsync(factory);
        factory.Time.Advance(Minute);
        await factory.Time.TimerAsync(Minute, Within);

        Assert.Equal(2, await RequestsAsync(factory));
    }

    private static async Task ArrangeAsync(FlushFactory factory)
    {
        factory.Time.SetUtcNow(new DateTimeOffset(2026, 11, 28, 14, 46, 0, TimeSpan.Zero));
        await StaffClient.SeedAsync(factory);
        factory.Key = await KeyAsync(factory, ApiScopes.ArticlesRead);
    }

    private static async Task CallAsync(FlushFactory factory)
    {
        using var product = Client(factory, factory.Key);
        using var response = await product.GetAsync(new Uri("/v1/articles", UriKind.Relative), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<int> RequestsAsync(ApiFactory factory)
    {
        using var staff = await StaffClient.OpenAsync(factory);
        var listed = await staff.GetFromJsonAsync<JsonElement>(new Uri("/admin/v1/api-clients", UriKind.Relative), TestContext.Current.CancellationToken);
        return Assert.Single(listed.GetProperty("items").EnumerateArray()).GetProperty("requests_24h").GetInt32();
    }

    private sealed class FlushFactory : ApiFactory
    {
        public SaveFault.Switch Fault { get; } = new();

        public string? Key { get; set; }

        protected override bool RunsUsageFlush => true;

        protected override void ConfigureServices(IServiceCollection services) =>
            services.AddScoped<IAppDbContext>(provider => new SaveFault(provider.GetRequiredService<KeryxDbContext>(), Fault));
    }
}
