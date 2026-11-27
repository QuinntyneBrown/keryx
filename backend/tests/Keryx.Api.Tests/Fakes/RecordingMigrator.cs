using Keryx.Infrastructure.Persistence.Schema;

namespace Keryx.Api.Tests.Fakes;

/// <summary>
/// Stands for the migrations: each call answers the next of <paramref name="applied"/>, then
/// none, and is counted, so a test proves both what <c>migrate</c> reports and that nothing else
/// ever migrates.
/// </summary>
public sealed class RecordingMigrator(params int[] applied) : IDatabaseMigrator
{
    private readonly Queue<int> _applied = new(applied);
    private int _calls;

    public int Calls => _calls;

    public Task<int> MigrateAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        return Task.FromResult(_applied.TryDequeue(out var count) ? count : 0);
    }
}
