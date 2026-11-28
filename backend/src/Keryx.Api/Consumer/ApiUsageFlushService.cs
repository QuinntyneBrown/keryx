using Keryx.Application.Clients;
using MediatR;

namespace Keryx.Api.Consumer;

/// <summary>
/// Flushes the consumer API's usage, counted in memory, to the database every 60 seconds by
/// <see cref="TimeProvider"/>, and once more as the API stops (L2-037). It checks nothing and
/// sends one <see cref="FlushApiUsage"/>; a flush that fails is logged by its exception type
/// alone, its counts wait for the next, and the timer goes on.
/// </summary>
public sealed partial class ApiUsageFlushService(IServiceScopeFactory scopes, TimeProvider time, ILogger<ApiUsageFlushService> logger) : BackgroundService
{
    /// <summary>How often usage is flushed: a crash loses at most this much of it.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        await FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Interval, time, stoppingToken).ConfigureAwait(false);
            await FlushAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task FlushAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new FlushApiUsage(), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // The type only: a message can carry a connection string.
            FlushFailed(logger, exception.GetType().Name);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "API usage could not be saved ({ExceptionType}); it is tried again with the next flush.")]
    private static partial void FlushFailed(ILogger logger, string exceptionType);
}
