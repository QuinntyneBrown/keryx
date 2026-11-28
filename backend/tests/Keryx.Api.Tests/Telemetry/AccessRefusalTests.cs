using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Headers;
using Keryx.Api.Tests.Fakes;
using Keryx.Api.Tests.Session;
using Keryx.Domain.Clients;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;

using static Keryx.Api.Tests.Consumer.ConsumerApi;

namespace Keryx.Api.Tests.Telemetry;

// Acceptance Test
// Traces to: L2-085 AC3
// Description: A refused request (401, 403) and a failed sign-in callback are each counted in
// keryx.auth.refused by status, reason and route template, and logged by the same fields only:
// no log line holds the key, the state or the cookie presented. What the API would export is
// kept in memory.
public sealed class AccessRefusalTests
{
    private const string Counter = "keryx.auth.refused";
    private const string BogusKey = "kx_live_bogusbogusbogusbogusbogusbogus";
    private const string ForgedState = "forged-state-value";

    [Fact]
    public async Task Given_refused_requests_and_a_failed_sign_in_When_they_are_answered_Then_each_is_counted_and_logged_without_the_credential()
    {
        using var factory = new Factory();
        await StaffClient.SeedAsync(factory);
        var eventsOnly = await KeyAsync(factory, ApiScopes.EventsRead);
        using var refusals = new Refusals(factory);

        using (var bogus = Client(factory, null))
        {
            bogus.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", BogusKey);
            Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(bogus, "/v1/articles"));
        }

        using (var scoped = Client(factory, eventsOnly))
        {
            Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(scoped, "/v1/articles"));
        }

        using (var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(anonymous, "/admin/v1/settings"));
            Assert.Equal(HttpStatusCode.Redirect, await StatusAsync(anonymous, $"/admin/v1/auth/callback?state={ForgedState}&code=abc"));
        }

        using (var member = await TestSession.OpenAsync(factory, "member-example"))
        {
            Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(member, "/admin/v1/articles/art_abcdefghijkl"));
        }

        using (var staff = await StaffClient.OpenAsync(factory))
        {
            // Past the session's idle limit.
            factory.Time.Advance(TimeSpan.FromMinutes(60));
            Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(staff, "/admin/v1/settings"));
        }

        // The counter is exported (by name: other hosts in this process export theirs too)...
        Assert.True(factory.Services.GetRequiredService<MeterProvider>().ForceFlush());
        Assert.Contains(factory.ExportedMetrics.Snapshot(), metric => metric.Name == Counter);
        // ...and this host counted each refusal once.
        Assert.Equal(
            [
                ("401", "invalid_api_key", "v1/articles", 1L),
                ("401", "not_signed_in", "admin/v1/settings", 1L),
                ("401", "session_expired", "admin/v1/settings", 1L),
                ("401", "signin_failed", "admin/v1/auth/callback", 1L),
                ("403", "insufficient_scope", "v1/articles", 1L),
                ("403", "not_staff", "admin/v1/articles/{id}", 1L),
            ],
            refusals.Counted().Order());
        Assert.True(factory.Services.GetRequiredService<LoggerProvider>().ForceFlush());
        var logs = factory.ExportedLogs.Snapshot().Select(Describe).ToList();
        Assert.Equal(6, logs.Count(line => line.Contains("refused", StringComparison.OrdinalIgnoreCase) && line.Contains("Reason=", StringComparison.Ordinal)));
        Assert.DoesNotContain(logs, line => line.Contains(BogusKey[..12], StringComparison.Ordinal)
            || line.Contains(eventsOnly[..12], StringComparison.Ordinal) || line.Contains(ForgedState, StringComparison.Ordinal));
    }

    private static async Task<HttpStatusCode> StatusAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
        return response.StatusCode;
    }

    /// <summary>
    /// The refusals this host counts. A listener filtered to the host's own meter factory, because
    /// OpenTelemetry subscribes to a meter by name and the tests run several hosts at once.
    /// </summary>
    private sealed class Refusals : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly Dictionary<(string, string, string), long> _counted = [];

        public Refusals(ApiFactory factory)
        {
            var meters = factory.Services.GetRequiredService<IMeterFactory>();
            _listener.InstrumentPublished = (instrument, listening) =>
            {
                if (instrument.Name == Counter && instrument.Meter.Scope == meters)
                {
                    listening.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
            {
                var named = tags.ToArray().ToDictionary(tag => tag.Key, tag => $"{tag.Value}", StringComparer.Ordinal);
                var key = (named["status"], named["reason"], named["http.route"]);
                lock (_counted)
                {
                    _counted[key] = _counted.GetValueOrDefault(key) + value;
                }
            });
            _listener.Start();
        }

        public List<(string, string, string, long)> Counted()
        {
            lock (_counted)
            {
                return [.. _counted.Select(pair => (pair.Key.Item1, pair.Key.Item2, pair.Key.Item3, pair.Value))];
            }
        }

        public void Dispose() => _listener.Dispose();
    }

    private static string Describe(LogRecord log) =>
        string.Join(" | ", [log.CategoryName, log.FormattedMessage, $"{log.Body}", .. (log.Attributes ?? []).Select(attribute => $"{attribute.Key}={attribute.Value}")]);

    private sealed class Factory : ApiFactory
    {
        public ExportedCollection<LogRecord> ExportedLogs { get; } = new();

        public ExportedCollection<Metric> ExportedMetrics { get; } = new();

        protected override void ConfigureServices(IServiceCollection services)
        {
            services.ConfigureOpenTelemetryLoggerProvider(logging => logging.AddInMemoryExporter(ExportedLogs));
            services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddInMemoryExporter(ExportedMetrics));
        }
    }
}
