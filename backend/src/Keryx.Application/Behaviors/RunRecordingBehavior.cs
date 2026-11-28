using System.Diagnostics;
using Keryx.Application.Abstractions;
using Keryx.Application.Telemetry;
using Keryx.Domain.Collection;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Keryx.Application.Behaviors;

/// <summary>A request that runs a worker: its run is recorded however it ends (L2-019).</summary>
public interface IWorkerRunCommand
{
    RunWorker Worker { get; }
}

/// <summary>
/// Records a worker's run that throws (L2-019 AC3): the run the worker's lease names is failed
/// with a generic reason and a correlation id, the current trace id or a new one, and the lease
/// is freed so the next scheduled run still starts. The log names the exception's type and the
/// correlation id, never its message. The exception is not rethrown: the worker goes on. Each
/// run is one trace, whose root span this starts (L2-094 AC3), so a failed run's correlation id
/// is its trace's id.
/// </summary>
public sealed partial class RunRecordingBehavior<TRequest, TResponse>(
    IServiceScopeFactory scopes, TimeProvider time, ILogger<RunRecordingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IWorkerRunCommand
{
    internal const string GenericReason = "The run stopped unexpectedly.";

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);
        using var span = RunTelemetry.StartRun(request.Worker);
        try
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            span?.SetStatus(ActivityStatusCode.Error);
            span?.SetTag("error.type", exception.GetType().FullName);
            var correlation = (Activity.Current?.TraceId ?? ActivityTraceId.CreateRandom()).ToHexString();
            RunFailed(logger, RunWordsFor(request.Worker), exception.GetType().Name, correlation);
            await RecordFailureAsync(request.Worker, correlation, cancellationToken).ConfigureAwait(false);
            return default!;
        }
    }

    /// <summary>In a scope of its own: the handler's context may hold the changes that failed.</summary>
    private async Task RecordFailureAsync(RunWorker worker, string correlation, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var lease = await db.RunLeases.SingleOrDefaultAsync(candidate => candidate.Worker == worker, cancellationToken).ConfigureAwait(false);
        if (lease?.ActiveRunId is not { } runId)
        {
            return;
        }

        var run = await db.Runs.SingleAsync(candidate => candidate.PublicId == runId, cancellationToken).ConfigureAwait(false);
        run.Fail(GenericReason, correlation, time.GetUtcNow());
        lease.Release(runId);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        // Alerts follow the saved outcome (L2-077 AC4).
        await scope.ServiceProvider.GetRequiredService<IPublisher>()
            .Publish(new Alerts.RunFinishedNotification(runId), cancellationToken).ConfigureAwait(false);
    }

    private static string RunWordsFor(RunWorker worker) => Runs.RunWords.Worker(worker);

    [LoggerMessage(Level = LogLevel.Error, Message = "A {Worker} run failed with {ExceptionType}; correlation id {CorrelationId}.")]
    private static partial void RunFailed(ILogger logger, string worker, string exceptionType, string correlationId);
}
