using Keryx.Application.Catalogue;
using Keryx.Domain;
using Keryx.Domain.Catalogue;
using Keryx.Domain.Collection;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Keryx.Worker;

/// <summary>
/// Retention's loop (L2-079). Once the database is ready it waits on <see cref="TimeProvider"/>
/// until the next 03:00 Toronto time, sends one <see cref="ApplyRetentionCommand"/>, and repeats.
/// It checks nothing itself; a run that throws is recorded as failed by the run-recording
/// behaviour, and one that fails before that is logged by its exception type alone, so the next
/// night's still runs and the other workers are untouched (L2-019 AC4).
/// </summary>
public sealed partial class RetentionWorker(
    WorkerReadinessGate gate, IServiceScopeFactory scopes, TimeProvider time, ILogger<RetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await gate.Ready.WaitAsync(stoppingToken).ConfigureAwait(false);
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = time.GetUtcNow();
            await Task.Delay(RetentionSchedule.NextAfter(now, TorontoTime.Zone) - now, time, stoppingToken).ConfigureAwait(false);
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ApplyRetentionCommand(RunTrigger.Schedule), stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                // The type only: a message can carry a connection string.
                RetentionFailed(logger, exception.GetType().Name);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Retention failed with {ExceptionType}; it runs again tomorrow at 03:00.")]
    private static partial void RetentionFailed(ILogger logger, string exceptionType);
}
