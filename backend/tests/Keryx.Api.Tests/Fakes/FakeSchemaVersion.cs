using Keryx.Infrastructure.Persistence.Schema;

namespace Keryx.Api.Tests.Fakes;

/// <summary>
/// Stands for a database in a state a test chooses: at the expected schema, behind it, ahead of
/// it or unreachable. It counts the times it is asked, and a test awaits a count.
/// </summary>
public sealed class FakeSchemaVersion(SchemaState state) : ISchemaVersion
{
    public static readonly SchemaState AtExpected = new(Reachable: true, "20261127000000_Initial", "20261127000000_Initial");
    public static readonly SchemaState Behind = new(Reachable: true, "20261127000000_Initial", "20261201000000_Next");
    public static readonly SchemaState NeverMigrated = new(Reachable: true, Applied: null, "20261127000000_Initial");
    public static readonly SchemaState Ahead = new(Reachable: true, "20261201000000_Next", "20261127000000_Initial");
    public static readonly SchemaState Unreachable = SchemaState.Unreachable("20261127000000_Initial");

    private readonly Lock _gate = new();
    private readonly List<(int Count, TaskCompletionSource Reached)> _waiters = [];
    private int _checks;

    /// <summary>What the database answers from now on.</summary>
    public SchemaState State { get; set; } = state;

    /// <summary>How many times the schema has been asked for.</summary>
    public int Checks
    {
        get
        {
            lock (_gate)
            {
                return _checks;
            }
        }
    }

    public Task<SchemaState> GetAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _checks++;
            foreach (var (_, reached) in _waiters.Where(waiter => waiter.Count <= _checks))
            {
                reached.TrySetResult();
            }
        }

        return Task.FromResult(State);
    }

    /// <summary>Completes once the schema has been asked for <paramref name="count"/> times.</summary>
    public async Task WhenCheckedAsync(int count, CancellationToken cancellationToken)
    {
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            if (_checks >= count)
            {
                return;
            }

            _waiters.Add((count, reached));
        }

        try
        {
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException($"The schema was asked for {Checks} time(s); the test awaited {count}.");
        }
    }
}
