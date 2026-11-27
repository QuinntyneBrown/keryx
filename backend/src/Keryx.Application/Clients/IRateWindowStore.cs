using Keryx.Domain.Clients;

namespace Keryx.Application.Clients;

/// <summary>
/// One rate window per caller, a client's id or an address (L2-038). An address is held only in
/// memory for the length of its window, and never stored or logged.
/// </summary>
public interface IRateWindowStore
{
    /// <summary>Takes one request from the caller's window, opening one when it has none or its last has passed.</summary>
    RateDecision TryTake(string caller, DateTimeOffset now, int limit);

    /// <summary>Whether the caller's window is full now; nothing is taken.</summary>
    bool IsFull(string caller, DateTimeOffset now);
}
