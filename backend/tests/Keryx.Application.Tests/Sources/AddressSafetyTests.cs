using System.Net;
using Keryx.Application.Sources;

namespace Keryx.Application.Tests.Sources;

// L2-024: Keryx never connects to a private, internal or reserved address.
public sealed class AddressSafetyTests
{
    [Theory]
    [InlineData("93.184.216.34")]
    [InlineData("8.8.8.8")]
    [InlineData("172.15.255.255")]
    [InlineData("172.32.0.1")]
    [InlineData("100.63.255.255")]
    [InlineData("2606:2800:220:1:248:1893:25c8:1946")]
    public void Given_a_public_address_When_it_is_checked_Then_it_is_public(string address) =>
        Assert.True(AddressSafety.IsPublic(IPAddress.Parse(address)));

    [Theory]
    [InlineData("127.0.0.1")] // loopback
    [InlineData("127.255.255.254")]
    [InlineData("10.0.0.5")] // RFC 1918
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")] // link-local and cloud metadata
    [InlineData("100.64.0.1")] // carrier-grade NAT
    [InlineData("100.127.255.255")]
    [InlineData("0.0.0.0")] // this network
    [InlineData("224.0.0.1")] // multicast
    [InlineData("240.0.0.1")] // reserved
    [InlineData("255.255.255.255")] // broadcast
    [InlineData("192.0.2.1")] // documentation
    [InlineData("198.18.0.1")] // benchmarking
    [InlineData("::1")] // IPv6 loopback
    [InlineData("::")] // unspecified
    [InlineData("fe80::1")] // link-local
    [InlineData("fc00::1")] // unique-local
    [InlineData("fd00:ec2::254")] // cloud metadata, unique-local
    [InlineData("ff02::1")] // multicast
    [InlineData("::ffff:10.0.0.1")] // IPv4-mapped, checked as IPv4
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("2001:db8::1")] // documentation
    public void Given_a_non_public_address_When_it_is_checked_Then_it_is_not_public(string address) =>
        Assert.False(AddressSafety.IsPublic(IPAddress.Parse(address)));
}
