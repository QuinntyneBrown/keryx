using Keryx.Application.Collection;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Keryx.Worker;

/// <summary>
/// The collection schedule's loop (L2-005). Once the database is ready it sends one tick, waits on
/// <see cref="TimeProvider"/> until the wake time the tick returns, and repeats. It checks nothing
/// itself. A tick that throws is logged by its exception type alone and tried again a minute
/// later, so the next slot still fires (L2-019 AC3).
/// </summary>
public sealed partial class CollectionScheduler(
    WorkerReadinessGate gate, IServiceScopeFactory scopes, TimeProvider time, ILogger<CollectionScheduler> logger) : BackgroundService
{
    /// <summary>How soon a failed tick is tried again.</summary>
    internal static readonly TimeSpan RetryAfterFailure = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await gate.Ready.WaitAsync(stoppingToken).ConfigureAwait(false);
        while (!stoppingToken.IsCancellationRequested)
        {
            var wait = RetryAfterFailure;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<ISender>()
                    .Send(new TickCollectionSchedule(), stoppingToken).ConfigureAwait(false);
                wait = result.NextWakeAt - time.GetUtcNow();
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                // The type only: a message can carry an address or a connection string.
                TickFailed(logger, exception.GetType().Name);
            }

            await Task.Delay(wait > TimeSpan.Zero ? wait : TimeSpan.Zero, time, stoppingToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "A collection schedule tick failed with {ExceptionType}; the schedule looks again in a minute.")]
    private static partial void TickFailed(ILogger logger, string exceptionType);
}
