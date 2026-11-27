using System.Net;
using System.Net.Sockets;
using Keryx.Application.Sources;

namespace Keryx.Infrastructure.Sources;

/// <summary>
/// The connections the source fetcher sends through. In production a socket handler that checks
/// each address as it connects and connects only to the address it checked, so a name that
/// resolves to a public address when checked and a private one later is never reached (L2-024
/// AC3). Redirects and cookies are off: the fetcher follows redirects itself.
/// </summary>
public sealed class SourceSockets(HttpMessageHandler handler)
{
    public HttpMessageHandler Handler { get; } = handler;

    public static SourceSockets Connect(IHostResolver resolver) => new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        UseProxy = false,
        AutomaticDecompression = DecompressionMethods.All,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        ConnectTimeout = TimeSpan.FromSeconds(15),
        ConnectCallback = async (context, cancellationToken) =>
        {
            var host = context.DnsEndPoint.Host.Trim('[', ']');
            IReadOnlyList<IPAddress> addresses = IPAddress.TryParse(host, out var literal)
                ? [literal]
                : await resolver.ResolveAsync(host, cancellationToken).ConfigureAwait(false);
            if (addresses.Count == 0 || !addresses.All(AddressSafety.IsPublic))
            {
                throw new UnsafeConnectionException();
            }

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(addresses[0], context.DnsEndPoint.Port, cancellationToken).ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        },
    });
}

/// <summary>A connection refused because its address is not public.</summary>
public sealed class UnsafeConnectionException : IOException
{
    public UnsafeConnectionException()
        : base("The address is not public.")
    {
    }

    public UnsafeConnectionException(string message)
        : base(message)
    {
    }

    public UnsafeConnectionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
