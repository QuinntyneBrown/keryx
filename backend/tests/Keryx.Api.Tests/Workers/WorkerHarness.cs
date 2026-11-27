using Keryx.Api.Tests.Fakes;
using Keryx.Infrastructure.Persistence.Schema;
using Keryx.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Time.Testing;

namespace Keryx.Api.Tests.Workers;

/// <summary>
/// The worker's own host, as <c>Program</c> builds it, against a database in the state a test
/// chooses, with the clock a test advances and one probe standing for the workers that wait on
/// the readiness gate. Nothing here sleeps: a test awaits a check, a log line or the probe.
/// </summary>
public sealed class WorkerHarness : IAsyncDisposable
{
    private readonly IHost _host;

    public WorkerHarness(SchemaState state, Action<IServiceCollection>? configure = null)
    {
        Schema = new FakeSchemaVersion(state);
        _host = WorkerHost.Build([], builder =>
        {
            // No OTLP endpoint, whatever the machine's environment says: a test reaches no network.
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["ConnectionStrings:Keryx"] = TestStore.UnusedConnection,
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = string.Empty,
            });
            builder.Logging.AddFilter<ConsoleLoggerProvider>(_ => false);
            builder.Logging.AddProvider(Logs);
            builder.Services.UseTestStore(TestStore.NewName());
            builder.Services.RemoveAll<TimeProvider>();
            builder.Services.AddSingleton<TimeProvider>(Time);
            builder.Services.RemoveAll<ISchemaVersion>();
            builder.Services.AddSingleton<ISchemaVersion>(Schema);
            builder.Services.RemoveAll<IDatabaseMigrator>();
            builder.Services.AddSingleton<IDatabaseMigrator>(Migrator);
            builder.Services.AddSingleton<ProbeWorker>();
            builder.Services.AddHostedService(provider => provider.GetRequiredService<ProbeWorker>());
            configure?.Invoke(builder.Services);
        });
    }

    public SignallingTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 27, 15, 0, 0, TimeSpan.Zero));

    public CapturedLogs Logs { get; } = new();

    public FakeSchemaVersion Schema { get; }

    /// <summary>The migrations, which the worker must never touch.</summary>
    public RecordingMigrator Migrator { get; } = new();

    public IServiceProvider Services => _host.Services;

    public WorkerReadinessGate Gate => _host.Services.GetRequiredService<WorkerReadinessGate>();

    /// <summary>A worker like any other: it awaits the gate, then runs.</summary>
    public ProbeWorker Probe => _host.Services.GetRequiredService<ProbeWorker>();

    public Task StartAsync() => _host.StartAsync(TestContext.Current.CancellationToken);

    /// <summary>Stops the host and waits for its services, so nothing is still about to happen.</summary>
    public Task StopAsync() => _host.StopAsync(TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync(CancellationToken.None);
        _host.Dispose();
    }
}

/// <summary>Stands for a scheduled worker: it runs nothing until the gate lets it.</summary>
public sealed class ProbeWorker(WorkerReadinessGate gate) : BackgroundService
{
    private readonly TaskCompletionSource _ran = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _runs;

    public int Runs => _runs;

    /// <summary>Completes when the probe has run; a timeout names how often the schema was asked.</summary>
    public async Task WhenRanAsync(FakeSchemaVersion schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        try
        {
            await _ran.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException($"The worker never ran. The schema was asked for {schema.Checks} time(s) and answered {schema.State.Status}.");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await gate.Ready.WaitAsync(stoppingToken);
        Interlocked.Increment(ref _runs);
        _ran.TrySetResult();
    }
}
