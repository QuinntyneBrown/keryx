using System.Threading.Channels;
using Microsoft.Extensions.Time.Testing;

namespace Keryx.Api.Tests.Fakes;

/// <summary>
/// The fixture's fake clock, which also announces each timer created on it, so a test can wait
/// until code under test is waiting on the clock before it advances it.
/// </summary>
public sealed class SignallingTimeProvider(DateTimeOffset start) : FakeTimeProvider(start)
{
    private readonly Channel<Announced> _timers = Channel.CreateUnbounded<Announced>();

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new Announced(base.CreateTimer(callback, state, dueTime, period), dueTime);
        if (dueTime != Timeout.InfiniteTimeSpan)
        {
            _timers.Writer.TryWrite(timer);
        }

        return timer;
    }

    /// <summary>
    /// Waits for a live timer due in exactly <paramref name="due"/>; timers already disposed (a
    /// timeout whose call has finished) are passed over. Names the timers seen if none arrives.
    /// </summary>
    public async Task<TimeSpan> TimerAsync(TimeSpan due, TimeSpan within)
    {
        using var timeout = new CancellationTokenSource(within);
        var seen = new List<string>();
        try
        {
            while (true)
            {
                var timer = await _timers.Reader.ReadAsync(timeout.Token);
                if (timer.Due == due && !timer.Disposed)
                {
                    return timer.Due;
                }

                seen.Add($"{timer.Due}{(timer.Disposed ? " (disposed)" : string.Empty)}");
            }
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"No timer due in {due} within {within}; created: [{string.Join(", ", seen)}]");
        }
    }

    private sealed class Announced(ITimer inner, TimeSpan due) : ITimer
    {
        public TimeSpan Due { get; } = due;

        public bool Disposed { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period) => inner.Change(dueTime, period);

        public void Dispose()
        {
            Disposed = true;
            inner.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return inner.DisposeAsync();
        }
    }
}
