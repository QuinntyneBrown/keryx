using System.Net;

namespace Keryx.Application.Sources;

/// <summary>
/// Whether a URL's host is safe to connect to (L2-024): a literal address must be public, and a
/// name must resolve only to public addresses; one non-public address refuses the whole host.
/// A name that resolves to nothing is not refused here: the fetch reports it.
/// </summary>
public sealed class SafeAddressCheck(IHostResolver resolver)
{
    public async Task<bool> IsSafeAsync(Uri address, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);
        var host = address.IdnHost.Trim('[', ']');
        if (IPAddress.TryParse(host, out var literal))
        {
            return AddressSafety.IsPublic(literal);
        }

        var resolved = await resolver.ResolveAsync(host, cancellationToken).ConfigureAwait(false);
        return resolved.All(AddressSafety.IsPublic);
    }
}
