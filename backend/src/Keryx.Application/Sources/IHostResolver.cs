using System.Net;

namespace Keryx.Application.Sources;

/// <summary>Resolves a host name to its addresses (DNS). Infrastructure implements it; tests answer from a table.</summary>
public interface IHostResolver
{
    /// <summary>Every address the host resolves to; none when it does not resolve.</summary>
    Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken);
}
