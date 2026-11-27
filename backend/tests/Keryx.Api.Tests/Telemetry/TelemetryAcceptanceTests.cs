// Acceptance Test
// Traces to: L2-094
// Description: The API and the worker emit traces, metrics and logs and run with no OTLP endpoint, and a request's span carries its route template, method and status and nothing of the URL

using System.Diagnostics;
using System.Net;
using Keryx.Api.Tests.Fakes;
using Keryx.Api.Tests.Probes;
using Keryx.Api.Tests.Workers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Keryx.Api.Tests.Telemetry;

public sealed class TelemetryAcceptanceTests(TelemetryAcceptanceTests.Factory factory) : IClassFixture<TelemetryAcceptanceTests.Factory>
{
    private const string Personal = "someone@example.com";
    private const string Token = "s3cr3t-token";

    /// <summary>The API with no OTLP endpoint. What it would export is kept in memory instead.</summary>
    public class Factory : ApiFactory
    {
        public ExportedCollection<Activity> ExportedSpans { get; } = new();

        public ExportedCollection<LogRecord> ExportedLogs { get; } = new();

        public ExportedCollection<Metric> ExportedMetrics { get; } = new();

        protected override void ConfigureServices(IServiceCollection services)
        {
            services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddInMemoryExporter(ExportedSpans));
            services.ConfigureOpenTelemetryLoggerProvider(logging => logging.AddInMemoryExporter(ExportedLogs));
            services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddInMemoryExporter(ExportedMetrics));
        }
    }

    /// <summary>The API of a deployment that names its service itself.</summary>
    public sealed class NamedFactory : Factory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("OTEL_SERVICE_NAME", "keryx-api-staging");
        }
    }

    // L2-094 AC1 and AC2: the route is its template, with the method and the status, and nothing else.
    [Fact]
    public async Task Given_no_OTLP_endpoint_When_a_routed_request_is_served_Then_its_span_carries_the_route_template_method_and_status_only()
    {
        var span = await RequestAndAwaitSpanAsync("/health/live", HttpStatusCode.OK);

        Assert.Equal("GET /health/live", span.DisplayName);
        Assert.Equal(["http.request.method", "http.response.status_code", "http.route"], TagKeys(span));
        Assert.Equal("/health/live", span.GetTagItem("http.route"));
        Assert.Equal("GET", span.GetTagItem("http.request.method"));
        Assert.Equal(200, span.GetTagItem("http.response.status_code"));
        AssertNothingOfTheUrl(span);
        Assert.Equal("keryx-api", ServiceName(factory.Services.GetRequiredService<TracerProvider>().GetResource()));
    }

    // A path that matches no route has no template: its span says nothing of the path at all.
    [Fact]
    public async Task Given_a_path_that_matches_no_route_When_it_is_requested_Then_its_span_carries_the_method_and_status_only()
    {
        var span = await RequestAndAwaitSpanAsync("/v1/articles/art_0190c0de", HttpStatusCode.NotFound);

        Assert.Equal("GET", span.DisplayName);
        Assert.Equal(["http.request.method", "http.response.status_code"], TagKeys(span));
        AssertNothingOfTheUrl(span);
        Assert.DoesNotContain(TagValues(span), value => value.Contains("0190c0de", StringComparison.Ordinal));
    }

    // An unhandled failure is named by its type; its message, which may quote anything, is not kept.
    [Fact]
    public async Task Given_an_unhandled_exception_When_its_request_is_traced_Then_the_span_names_the_error_type_and_keeps_no_message()
    {
        var span = await RequestAndAwaitSpanAsync("/test/probe/throw", HttpStatusCode.InternalServerError);

        Assert.Equal(["error.type", "http.request.method", "http.response.status_code", "http.route"], TagKeys(span));
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Null(span.StatusDescription);
        Assert.Empty(span.Events);
        Assert.DoesNotContain(TagValues(span), value => value.Contains("hunter2", StringComparison.Ordinal));
        AssertNothingOfTheUrl(span);
    }

    // L2-094 AC5: the reference an error carries is the id of the trace that was recorded.
    [Fact]
    public async Task Given_an_error_response_When_its_trace_is_recorded_Then_trace_id_names_that_trace()
    {
        var traceId = ActivityTraceId.CreateRandom();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/v1/no-such-thing", UriKind.Relative));
        request.Headers.Add("traceparent", $"00-{traceId}-{ActivitySpanId.CreateRandom()}-01");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var span = await factory.ExportedSpans.WaitForAsync(
            candidate => candidate.Kind == ActivityKind.Server && candidate.TraceId == traceId, Describe, "the server span of the error");

        Assert.Contains($"\"trace_id\":\"{span.TraceId}\"", body, StringComparison.Ordinal);
    }

    // L2-094 AC1, logs: the API's own lines are exported, and none carries a URL.
    [Fact]
    public async Task Given_no_OTLP_endpoint_When_requests_are_served_Then_the_APIs_logs_are_recorded_and_none_carries_the_url()
    {
        await RequestAndAwaitSpanAsync("/health/live", HttpStatusCode.OK);
        await RequestAndAwaitSpanAsync("/v1/articles/art_0190c0de", HttpStatusCode.NotFound);

        var started = await factory.ExportedLogs.WaitForAsync(
            log => log.CategoryName == "Keryx.Api.Health.SchemaStatusLogger", Describe, "the line the API logs as it starts");

        Assert.Equal("keryx-api", ServiceName(factory.Services.GetRequiredService<LoggerProvider>().GetResource()));
        Assert.Contains("ready", $"{started.Body}", StringComparison.Ordinal);
        foreach (var log in factory.ExportedLogs.Snapshot())
        {
            var text = Describe(log);
            Assert.DoesNotContain("someone", text, StringComparison.Ordinal);
            Assert.DoesNotContain(Token, text, StringComparison.Ordinal);
            Assert.DoesNotContain("/health/live", text, StringComparison.Ordinal);
            Assert.DoesNotContain("0190c0de", text, StringComparison.Ordinal);
        }
    }

    // L2-094 AC1 and AC2, metrics: a request is measured, by its route template and never its path.
    [Fact]
    public async Task Given_no_OTLP_endpoint_When_a_request_is_served_Then_its_duration_is_measured_by_route_template()
    {
        // A host of its own: what it would export is flushed once, here, and read once.
        await using var api = new Factory();

        // The duration is recorded just before the span stops, on the request's own thread: once
        // the span has been exported, the measurement has been made.
        await RequestAndAwaitSpanAsync(api, "/test/probe/items/0190c0de", HttpStatusCode.OK);
        Assert.True(api.Services.GetRequiredService<MeterProvider>().ForceFlush());

        // The SDK hears a meter by its name, from every host in the process, and a host disposed
        // by another test ends the metric they shared. This host's requests are then in one of
        // several metrics of the same name: read them all.
        var routes = new List<string>();
        foreach (var duration in api.ExportedMetrics.Snapshot().Where(metric => metric.Name == "http.server.request.duration"))
        {
            foreach (ref readonly var point in duration.GetMetricPoints())
            {
                foreach (var tag in point.Tags)
                {
                    var value = $"{tag.Value}";
                    Assert.DoesNotContain(Token, value, StringComparison.Ordinal);
                    Assert.DoesNotContain("0190c0de", value, StringComparison.Ordinal);
                    if (tag.Key == "http.route")
                    {
                        routes.Add(value);
                    }
                }
            }
        }

        Assert.Contains(ProbeController.ItemRoute, routes);
    }

    [Fact]
    public async Task Given_a_deployment_that_names_its_service_When_the_API_runs_Then_its_telemetry_carries_that_name()
    {
        await using var named = new NamedFactory();
        using var client = named.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("keryx-api-staging", ServiceName(named.Services.GetRequiredService<TracerProvider>().GetResource()));
    }

    // L2-094 AC1, the worker: it starts, lets its workers run and stops with no endpoint, and its lines are recorded.
    [Fact]
    public async Task Given_no_OTLP_endpoint_When_the_worker_starts_and_stops_Then_it_runs_without_error_and_its_logs_are_recorded()
    {
        var logs = new ExportedCollection<LogRecord>();
        await using var worker = new WorkerHarness(
            FakeSchemaVersion.AtExpected,
            configure: services => services.ConfigureOpenTelemetryLoggerProvider(logging => logging.AddInMemoryExporter(logs)));

        await worker.StartAsync();
        await worker.Probe.WhenRanAsync(worker.Schema);
        var ready = await logs.WaitForAsync(
            log => log.CategoryName == "Keryx.Worker.WorkerReadinessGate", Describe, "the line the worker logs when the database is ready");
        var serviceName = ServiceName(worker.Services.GetRequiredService<LoggerProvider>().GetResource());
        await worker.StopAsync();

        Assert.Contains("ready", $"{ready.Body}", StringComparison.Ordinal);
        Assert.Equal("keryx-worker", serviceName);
        Assert.Equal(1, worker.Probe.Runs);
    }

    private Task<Activity> RequestAndAwaitSpanAsync(string path, HttpStatusCode expected) =>
        RequestAndAwaitSpanAsync(factory, path, expected);

    private static async Task<Activity> RequestAndAwaitSpanAsync(Factory api, string path, HttpStatusCode expected)
    {
        // The request names its own trace, so the test can tell its span from its neighbours'.
        var traceId = ActivityTraceId.CreateRandom();
        using var client = api.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get, new Uri($"{path}?email={Uri.EscapeDataString(Personal)}&token={Token}", UriKind.Relative));
        request.Headers.Add("traceparent", $"00-{traceId}-{ActivitySpanId.CreateRandom()}-01");
        request.Headers.Add("X-Api-Key", Token);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(expected, response.StatusCode);

        // The span ends after the response is written: await it, never sleep.
        return await api.ExportedSpans.WaitForAsync(
            span => span.Kind == ActivityKind.Server && span.TraceId == traceId, Describe, $"the server span of {path}");
    }

    private static string[] TagKeys(Activity span) => [.. span.TagObjects.Select(tag => tag.Key).Order(StringComparer.Ordinal)];

    private static string[] TagValues(Activity span) => [.. span.TagObjects.Select(tag => $"{tag.Value}")];

    private static string Describe(Activity span) => $"{span.Kind} {span.DisplayName} {{{string.Join(", ", TagKeys(span))}}}";

    private static string Describe(LogRecord log) =>
        string.Join(" | ", [log.CategoryName, log.FormattedMessage, $"{log.Body}", .. (log.Attributes ?? []).Select(attribute => $"{attribute.Key}={attribute.Value}")]);

    private static string? ServiceName(Resource resource) =>
        resource.Attributes.Where(attribute => attribute.Key == "service.name").Select(attribute => $"{attribute.Value}").SingleOrDefault();

    private static void AssertNothingOfTheUrl(Activity span)
    {
        var text = $"{span.DisplayName} {string.Join(" ", TagValues(span))}";
        Assert.DoesNotContain("someone", text, StringComparison.Ordinal);
        Assert.DoesNotContain(Token, text, StringComparison.Ordinal);
        Assert.DoesNotContain("email", text, StringComparison.Ordinal);
        Assert.DoesNotContain("?", text, StringComparison.Ordinal);
    }
}
