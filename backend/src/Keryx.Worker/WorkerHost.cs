using Keryx.Application;
using Keryx.Infrastructure;
using Keryx.Infrastructure.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Keryx.Worker;

/// <summary>The composition of the worker process.</summary>
public static class WorkerHost
{
    /// <summary>
    /// Builds the worker's host. <paramref name="configure"/> lets a test put fakes behind the
    /// contracts; the process passes none.
    /// </summary>
    public static IHost Build(string[] args, Action<HostApplicationBuilder>? configure = null)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddSourceFetching();
        builder.Services.AddMail();
        builder.Services.AddKeryxTelemetry(builder.Configuration, KeryxTelemetry.WorkerServiceName);

        // One gate, which every worker awaits before its first schedule.
        builder.Services.AddSingleton<WorkerReadinessGate>();
        builder.Services.AddHostedService(provider => provider.GetRequiredService<WorkerReadinessGate>());

        // Each worker is its own hosted service with its own loop, so one failing stops no other.
        builder.Services.AddHostedService<CollectionScheduler>();
        builder.Services.AddHostedService<CollectionRunner>();
        builder.Services.AddHostedService<AlertDigestWorker>();
        builder.Services.AddHostedService<RetentionWorker>();

        configure?.Invoke(builder);
        return builder.Build();
    }
}
