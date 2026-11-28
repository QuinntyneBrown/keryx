using Keryx.Infrastructure.Mail;
using MailKit.Security;

namespace Keryx.Application.Tests.Hosting;

// Traces to: L2-077 AC6
// One case per row: how the mail service is reached. Every connection is encrypted: port 465 from
// the first byte, any other port by a STARTTLS the server must offer.
public sealed class MailSocketTests
{
    [Theory]
    [InlineData(465, SecureSocketOptions.SslOnConnect)]
    [InlineData(587, SecureSocketOptions.StartTls)]
    [InlineData(25, SecureSocketOptions.StartTls)]
    [InlineData(2525, SecureSocketOptions.StartTls)]
    public void SocketOptionsFor(int port, SecureSocketOptions expected) =>
        Assert.Equal(expected, SmtpMailSender.SocketOptionsFor(port));
}
