using System.Collections.Concurrent;
using System.Net;
using Keryx.Application.Sources;

namespace Keryx.Api.Tests.Fakes;

/// <summary>
/// DNS for the tests: a host resolves to the addresses a test gave it, and any other host to one
/// public address, so no test asks a real name server.
/// </summary>
public sealed class FakeHostResolver : IHostResolver
{
    private static readonly IPAddress Public = IPAddress.Parse("93.184.216.34");
    private readonly ConcurrentDictionary<string, IPAddress[]> _hosts = new(StringComparer.OrdinalIgnoreCase);

    public void Answer(string host, params string[] addresses) =>
        _hosts[host] = [.. addresses.Select(IPAddress.Parse)];

    public Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<IPAddress>>(_hosts.TryGetValue(host, out var addresses) ? addresses : [Public]);
}
