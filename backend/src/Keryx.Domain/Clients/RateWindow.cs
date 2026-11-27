namespace Keryx.Domain.Clients;

/// <summary>
/// One caller's fixed window of a minute (L2-038): it opens at the caller's first request and
/// allows <see cref="Limit"/> requests until a minute has passed, then opens again. It is not
/// thread-safe: its store takes it under a lock.
/// </summary>
public sealed class RateWindow
{
    public static readonly TimeSpan Length = TimeSpan.FromMinutes(1);

    private RateWindow(DateTimeOffset start, int limit)
    {
        Start = start;
        Limit = limit;
    }

    public DateTimeOffset Start { get; private set; }

    public int Limit { get; }

    public int Taken { get; private set; }

    public static RateWindow Open(DateTimeOffset now, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        return new RateWindow(now, limit);
    }

    /// <summary>Takes one request when the window has room, opening a new window once this one has passed.</summary>
    public RateDecision TryTake(DateTimeOffset now)
    {
        if (HasPassed(now))
        {
            Start = now;
            Taken = 0;
        }

        var reset = SecondsUntilReset(now);
        if (Taken >= Limit)
        {
            return new RateDecision(false, Limit, 0, reset, reset);
        }

        Taken++;
        return new RateDecision(true, Limit, Limit - Taken, reset, 0);
    }

    /// <summary>Whether a request now would be refused; nothing is taken.</summary>
    public bool IsFull(DateTimeOffset now) => !HasPassed(now) && Taken >= Limit;

    /// <summary>Whether the window is over, so its store may forget it.</summary>
    public bool HasPassed(DateTimeOffset now) => now >= Start + Length;

    private int SecondsUntilReset(DateTimeOffset now) => (int)Math.Ceiling((Start + Length - now).TotalSeconds);
}

/// <summary>A rate decision and the numbers a response carries: <c>RateLimit-*</c>, and <c>Retry-After</c> when refused.</summary>
public sealed record RateDecision(bool Allowed, int Limit, int Remaining, int ResetSeconds, int RetryAfterSeconds);
