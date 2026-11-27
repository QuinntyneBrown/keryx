using System.Net;
using System.Net.Sockets;

namespace Keryx.Application.Sources;

/// <summary>
/// Whether Keryx may connect to an address (L2-024): only a public unicast address. Loopback,
/// private (RFC 1918), carrier-grade NAT, link-local (cloud metadata included), unique-local,
/// multicast, broadcast, unspecified, documentation, benchmarking and reserved ranges are
/// refused. An IPv4-mapped IPv6 address is checked as the IPv4 address it carries.
/// </summary>
public static class AddressSafety
{
    private static readonly (IPAddress Network, int Bits)[] RefusedV4 =
    [
        (IPAddress.Parse("0.0.0.0"), 8), // this network
        (IPAddress.Parse("10.0.0.0"), 8), // private
        (IPAddress.Parse("100.64.0.0"), 10), // carrier-grade NAT
        (IPAddress.Parse("127.0.0.0"), 8), // loopback
        (IPAddress.Parse("169.254.0.0"), 16), // link-local, cloud metadata
        (IPAddress.Parse("172.16.0.0"), 12), // private
        (IPAddress.Parse("192.0.0.0"), 24), // IETF protocol assignments
        (IPAddress.Parse("192.0.2.0"), 24), // documentation
        (IPAddress.Parse("192.88.99.0"), 24), // 6to4 relay
        (IPAddress.Parse("192.168.0.0"), 16), // private
        (IPAddress.Parse("198.18.0.0"), 15), // benchmarking
        (IPAddress.Parse("198.51.100.0"), 24), // documentation
        (IPAddress.Parse("203.0.113.0"), 24), // documentation
        (IPAddress.Parse("224.0.0.0"), 4), // multicast
        (IPAddress.Parse("240.0.0.0"), 4), // reserved, broadcast
    ];

    private static readonly (IPAddress Network, int Bits)[] RefusedV6 =
    [
        (IPAddress.Parse("::"), 127), // unspecified and loopback
        (IPAddress.Parse("64:ff9b:1::"), 48), // local-use translation
        (IPAddress.Parse("100::"), 64), // discard
        (IPAddress.Parse("2001:db8::"), 32), // documentation
        (IPAddress.Parse("fc00::"), 7), // unique-local, cloud metadata
        (IPAddress.Parse("fe80::"), 10), // link-local
        (IPAddress.Parse("ff00::"), 8), // multicast
    ];

    public static bool IsPublic(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        var refused = address.AddressFamily == AddressFamily.InterNetwork ? RefusedV4 : RefusedV6;
        return !refused.Any(range => Contains(range.Network, range.Bits, address));
    }

    private static bool Contains(IPAddress network, int bits, IPAddress address)
    {
        var prefix = network.GetAddressBytes();
        var candidate = address.GetAddressBytes();
        if (prefix.Length != candidate.Length)
        {
            return false;
        }

        for (var i = 0; i < prefix.Length && bits > 0; i++, bits -= 8)
        {
            var mask = bits >= 8 ? 0xFF : (byte)(0xFF << (8 - bits));
            if ((prefix[i] & mask) != (candidate[i] & mask))
            {
                return false;
            }
        }

        return true;
    }
}
