using Keryx.Application.Alerts;
using Keryx.Domain;
using Keryx.Domain.Alerts;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Keryx.Worker;

/// <summary>
/// The daily digest's loop (L2-078). Once the database is ready it waits on
/// <see cref="TimeProvider"/> until the next 08:00 Toronto time, sends one
/// <see cref="SendHeldArticlesDigestCommand"/>, and repeats. It checks nothing itself; a digest
/// that throws is recorded as its run by the run-recording behaviour, and one that fails before
/// that is logged by its exception type alone, so the next morning's still goes.
/// </summary>
public sealed partial class AlertDigestWorker(
    WorkerReadinessGate gate, IServiceScopeFactory scopes, TimeProvider time, ILogger<AlertDigestWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await gate.Ready.WaitAsync(stoppingToken).ConfigureAwait(false);
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = time.GetUtcNow();
            await Task.Delay(DigestSchedule.NextAfter(now, TorontoTime.Zone) - now, time, stoppingToken).ConfigureAwait(false);
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ISender>().Send(new SendHeldArticlesDigestCommand(), stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                // The type only: a message can carry an address or a connection string.
                DigestFailed(logger, exception.GetType().Name);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The daily digest failed with {ExceptionType}; the next is sent tomorrow at 08:00.")]
    private static partial void DigestFailed(ILogger logger, string exceptionType);
}
