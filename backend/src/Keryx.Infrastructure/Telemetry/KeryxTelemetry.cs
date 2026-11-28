using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Keryx.Infrastructure.Telemetry;

/// <summary>
/// OpenTelemetry for the API and the worker (L2-094): traces, metrics and logs under one
/// service name, exported by OTLP only when an endpoint is configured. With none, a process runs
/// the same and exports nothing. Settings: tools/deploy/configuration.md.
/// </summary>
public static class KeryxTelemetry
{
    /// <summary>The API's service name unless <c>OTEL_SERVICE_NAME</c> gives another.</summary>
    public const string ApiServiceName = "keryx-api";

    /// <summary>The worker's service name unless <c>OTEL_SERVICE_NAME</c> gives another.</summary>
    public const string WorkerServiceName = "keryx-worker";

    private const string OtlpEndpoint = "OTEL_EXPORTER_OTLP_ENDPOINT";
    private const string ServiceName = "OTEL_SERVICE_NAME";

    /// <summary>
    /// Registers the OpenTelemetry SDK. A process adds its own instrumentation through
    /// <paramref name="configureTracing"/> and <paramref name="configureMetrics"/>, so this
    /// assembly and the worker carry no web dependency. Every span passes
    /// <see cref="TelemetryRedactionProcessor"/> before any exporter sees it.
    /// </summary>
    public static IServiceCollection AddKeryxTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string defaultServiceName,
        Action<TracerProviderBuilder>? configureTracing = null,
        Action<MeterProviderBuilder>? configureMetrics = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Read here because a name added to the resource wins over the SDK's own reading of the variable.
        var serviceName = configuration[ServiceName] is { Length: > 0 } name ? name : defaultServiceName;
        var telemetry = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing.AddSource(Keryx.Application.Telemetry.RunTelemetry.SourceName);
                configureTracing?.Invoke(tracing);
                tracing.AddProcessor(new TelemetryRedactionProcessor());
            })
            .WithMetrics(metrics => configureMetrics?.Invoke(metrics))
            .WithLogging();

        // The exporter reads the endpoint, the protocol and the rest of the OTEL_* variables itself.
        if (!string.IsNullOrWhiteSpace(configuration[OtlpEndpoint]))
        {
            telemetry.UseOtlpExporter();
        }

        return services;
    }
}
