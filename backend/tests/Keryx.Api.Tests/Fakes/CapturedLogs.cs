using Microsoft.Extensions.Logging;

namespace Keryx.Api.Tests.Fakes;

/// <summary>One line a process logged.</summary>
public sealed record LogLine(LogLevel Level, string Category, string Message);

/// <summary>
/// Keeps every line a host logs, so a test reads what an operator would. A test awaits a line
/// rather than sleeping; when none arrives in time the failure names the lines that did.
/// </summary>
public sealed class CapturedLogs : ILoggerProvider
{
    private readonly Lock _gate = new();
    private readonly List<LogLine> _lines = [];
    private readonly List<(Func<LogLine, bool> Wanted, TaskCompletionSource<LogLine> Arrived)> _waiters = [];

    /// <summary>The lines logged so far, oldest first.</summary>
    public IReadOnlyList<LogLine> Lines
    {
        get
        {
            lock (_gate)
            {
                return [.. _lines];
            }
        }
    }

    /// <summary>The lines of one category at <paramref name="level"/> or above.</summary>
    public IReadOnlyList<LogLine> Of(string category, LogLevel level = LogLevel.Information) =>
        [.. Lines.Where(line => line.Category == category && line.Level >= level)];

    /// <summary>Completes with the first line that matches, whether already logged or not yet.</summary>
    public async Task<LogLine> WaitForAsync(Func<LogLine, bool> wanted, CancellationToken cancellationToken)
    {
        var arrived = new TaskCompletionSource<LogLine>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            if (_lines.FirstOrDefault(wanted) is { } already)
            {
                return already;
            }

            _waiters.Add((wanted, arrived));
        }

        try
        {
            return await arrived.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            var seen = string.Join(Environment.NewLine, Lines.Select(line => $"  {line.Level} {line.Category}: {line.Message}"));
            throw new TimeoutException($"The awaited log line did not arrive. Lines logged:{Environment.NewLine}{seen}");
        }
    }

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void Dispose()
    {
    }

    private void Add(LogLine line)
    {
        lock (_gate)
        {
            _lines.Add(line);
            foreach (var (_, arrived) in _waiters.Where(waiter => waiter.Wanted(line)))
            {
                arrived.TrySetResult(line);
            }
        }
    }

    private sealed class Logger(CapturedLogs logs, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            logs.Add(new LogLine(logLevel, category, formatter(state, exception)));
    }
}
