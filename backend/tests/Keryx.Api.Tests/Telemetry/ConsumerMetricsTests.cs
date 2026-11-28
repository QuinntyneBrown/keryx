using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using Keryx.Api.Tests.Fakes;
using Keryx.Api.Tests.Session;
using Keryx.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

using static Keryx.Api.Tests.Consumer.ConsumerApi;

namespace Keryx.Api.Tests.Telemetry;

// Acceptance Test
// Traces to: L2-094 AC4
// Description: The consumer API's requests are measured by route template and client id: how
// many, how long, and with what status or error type. The client is named by its public id,
// never by its key or key prefix, on the request's measurement and on its span.
public sealed class ConsumerMetricsTests
{
    private const string Duration = "http.server.request.duration";

    [Fact]
    public async Task Given_two_clients_When_they_call_the_consumer_API_Then_each_request_is_measured_and_traced_by_route_template_and_client()
    {
        using var factory = new Factory();
        await StaffClient.SeedAsync(factory);
        var first = await KeyAsync(factory, ApiScopes.ArticlesRead);
        var second = await KeyAsync(factory, ApiScopes.ArticlesRead);
        var (firstId, secondId) = await ClientIdsAsync(factory);
        using var measured = new HostMeasurements(factory.Services.GetRequiredService<IMeterFactory>(), Duration);

        using (var client = Client(factory, first))
        {
            Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, "/v1/articles"));
            Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, "/v1/articles?limit=5"));
        }

        using (var client = Client(factory, second))
        {
            Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(client, "/v1/articles/art_abcdefghijkl"));
        }

        using (var anonymous = Client(factory, null))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(anonymous, "/v1/articles"));
        }

        // The measurement is recorded as the request ends, after the response: await it.
        var consumer = await EventuallyAsync(() => measured.Of(Duration).Where(request => request.Tags.GetValueOrDefault("http.route", "").StartsWith("v1/", StringComparison.Ordinal)).ToList(), 4);
        Assert.Equal(
            [
                ("200", "v1/articles", firstId),
                ("200", "v1/articles", firstId),
                ("401", "v1/articles", "none"),
                ("404", "v1/articles/{id}", secondId),
            ],
            consumer.Select(request => (request.Tags["http.response.status_code"], request.Tags["http.route"], request.Tags.GetValueOrDefault("keryx.client.id", "none")))
                .Order());
        Assert.DoesNotContain(consumer, request => request.Tags.Values.Any(value => value.Contains(first[..12], StringComparison.Ordinal) || value.Contains(second[..12], StringComparison.Ordinal)));

        // Spans are exported from every host in this process, so this test's are told by its clients.
        var spans = factory.Spans.Snapshot()
            .Where(span => span.Kind == ActivityKind.Server && span.GetTagItem("keryx.client.id") is string id && (id == firstId || id == secondId))
            .ToList();
        Assert.Equal(
            [("v1/articles", firstId), ("v1/articles", firstId), ("v1/articles/{id}", secondId)],
            spans.Select(span => ($"{span.GetTagItem("http.route")}", $"{span.GetTagItem("keryx.client.id")}")).Order());
    }

    private static async Task<List<HostMeasurements.Measured>> EventuallyAsync(Func<List<HostMeasurements.Measured>> read, int count)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var seen = read();
        while (seen.Count < count)
        {
            Assert.False(timeout.IsCancellationRequested, $"Only {seen.Count} of {count} consumer requests were measured.");
            await Task.Yield();
            seen = read();
        }

        return seen;
    }

    private static async Task<(string First, string Second)> ClientIdsAsync(ApiFactory factory)
    {
        await using var store = TestStore.Open(factory.Store);
        var ids = await store.ApiClients.OrderBy(client => client.Id).Select(client => client.PublicId).ToListAsync(TestContext.Current.CancellationToken);
        return (ids[0], ids[1]);
    }

    private static async Task<HttpStatusCode> StatusAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
        return response.StatusCode;
    }

    private sealed class Factory : ApiFactory
    {
        public ExportedCollection<Activity> Spans { get; } = new();

        protected override void ConfigureServices(IServiceCollection services) =>
            services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddInMemoryExporter(Spans));
    }
}
