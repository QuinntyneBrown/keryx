using Keryx.Application.Collection;
using Keryx.Domain.Collection;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Keryx.Worker;

/// <summary>
/// Executes collection runs (L2-007). Once the database is ready it records any run a stop cut
/// short as interrupted (L2-019 AC5), then sends one pickup every <see cref="PickupInterval"/>,
/// waited on <see cref="TimeProvider"/>. It checks nothing itself: the pickup runs the pending run
/// if there is one, and a run that throws is recorded by the run-recording behaviour. It is a
/// separate loop from the schedule, so a slot that arrives during a long run is still seen.
/// </summary>
public sealed partial class CollectionRunner(
    WorkerReadinessGate gate, IServiceScopeFactory scopes, TimeProvider time, ILogger<CollectionRunner> logger) : BackgroundService
{
    /// <summary>How often the runner looks for a pending run.</summary>
    internal static readonly TimeSpan PickupInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await gate.Ready.WaitAsync(stoppingToken).ConfigureAwait(false);
        await SendAsync(new RecoverInterruptedRuns(RunWorker.Collection), stoppingToken).ConfigureAwait(false);
        while (!stoppingToken.IsCancellationRequested)
        {
            await SendAsync(new RunCollectionCommand(), stoppingToken).ConfigureAwait(false);
            await Task.Delay(PickupInterval, time, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task SendAsync(IRequest request, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(request, stoppingToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            // Reached only when even recording failed, such as the database being away; the type only.
            PickupFailed(logger, exception.GetType().Name);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "A collection pickup failed with {ExceptionType}; the runner looks again shortly.")]
    private static partial void PickupFailed(ILogger logger, string exceptionType);
}
