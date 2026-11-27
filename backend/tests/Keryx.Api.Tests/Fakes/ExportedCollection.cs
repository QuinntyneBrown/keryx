using System.Collections;

namespace Keryx.Api.Tests.Fakes;

/// <summary>
/// Where an in-memory OpenTelemetry exporter puts what a process exports, in place of a
/// collector on the network. A span is exported after its response is written, so a test awaits
/// the item it wants; when none arrives in time the failure names the items that did.
/// </summary>
public sealed class ExportedCollection<T> : ICollection<T>
{
    private readonly Lock _gate = new();
    private readonly List<T> _items = [];
    private readonly List<(Func<T, bool> Wanted, TaskCompletionSource<T> Arrived)> _waiters = [];

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _items.Count;
            }
        }
    }

    public bool IsReadOnly => false;

    /// <summary>What has been exported so far, oldest first.</summary>
    public IReadOnlyList<T> Snapshot()
    {
        lock (_gate)
        {
            return [.. _items];
        }
    }

    /// <summary>Completes with the first item that matches, whether already exported or not yet.</summary>
    public async Task<T> WaitForAsync(Func<T, bool> wanted, Func<T, string> describe, string what)
    {
        ArgumentNullException.ThrowIfNull(wanted);
        var arrived = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            foreach (var item in _items)
            {
                if (wanted(item))
                {
                    return item;
                }
            }

            _waiters.Add((wanted, arrived));
        }

        try
        {
            return await arrived.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        }
        catch (TimeoutException)
        {
            var seen = string.Join(Environment.NewLine, Snapshot().Select(item => $"  {describe(item)}"));
            throw new TimeoutException($"Nothing exported was {what}. Exported:{Environment.NewLine}{seen}");
        }
    }

    public void Add(T item)
    {
        lock (_gate)
        {
            _items.Add(item);
            foreach (var (_, arrived) in _waiters.Where(waiter => waiter.Wanted(item)))
            {
                arrived.TrySetResult(item);
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _items.Clear();
        }
    }

    public bool Contains(T item) => Snapshot().Contains(item);

    public void CopyTo(T[] array, int arrayIndex) => Snapshot().ToList().CopyTo(array, arrayIndex);

    public bool Remove(T item)
    {
        lock (_gate)
        {
            return _items.Remove(item);
        }
    }

    public IEnumerator<T> GetEnumerator() => Snapshot().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
