using Keryx.Infrastructure.Persistence.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Keryx.Worker;

/// <summary>
/// What every worker awaits before its first schedule (L2-095). While the database is not at
/// the schema this build expects the worker runs nothing, rather than failing mid-run: the gate
/// says why once, asks again every <see cref="RecheckInterval"/> and opens when the database is
/// ready. It never migrates, and it serves no endpoint: it reports through the log.
/// </summary>
public sealed class WorkerReadinessGate(IServiceScopeFactory scopes, TimeProvider time, ILogger<WorkerReadinessGate> logger)
    : BackgroundService
{
    /// <summary>How long the gate waits between two checks, on the clock of <see cref="TimeProvider"/>.</summary>
    public static readonly TimeSpan RecheckInterval = TimeSpan.FromSeconds(30);

    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Completes when the database is ready, and is cancelled if the worker stops first. A
    /// worker awaits it before its first schedule.
    /// </summary>
    public Task Ready => _ready.Task;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Created before the first check, so the interval runs from the check and not from its answer.
            using var timer = new PeriodicTimer(RecheckInterval, time);
            SchemaStatus? said = null;
            do
            {
                var status = await CheckAsync(stoppingToken).ConfigureAwait(false);
                if (status != said)
                {
                    // The reason is said once, and again only when it changes.
                    SchemaStatusLog.Report(logger, status);
                    said = status;
                }

                if (status == SchemaStatus.Ready)
                {
                    _ready.TrySetResult();
                    return;
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _ready.TrySetCanceled(stoppingToken);
            throw;
        }
    }

    private async Task<SchemaStatus> CheckAsync(CancellationToken cancellationToken)
    {
        // A scope for each check: the gate outlives any one unit of work.
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var state = await scope.ServiceProvider.GetRequiredService<ISchemaVersion>().GetAsync(cancellationToken).ConfigureAwait(false);
            return state.Status;
        }
    }
}
