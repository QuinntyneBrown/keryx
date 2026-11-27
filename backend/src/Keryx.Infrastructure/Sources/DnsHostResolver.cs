using System.Net;
using System.Net.Sockets;
using Keryx.Application.Sources;

namespace Keryx.Infrastructure.Sources;

/// <summary>Resolves a host through the system's DNS. A name that does not resolve has no addresses.</summary>
internal sealed class DnsHostResolver : IHostResolver
{
    public async Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        try
        {
            return await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
        }
        catch (SocketException)
        {
            return [];
        }
    }
}
