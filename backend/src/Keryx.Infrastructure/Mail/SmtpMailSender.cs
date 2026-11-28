using Keryx.Application.Alerts;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Keryx.Infrastructure.Mail;

/// <summary>
/// Sends alerts through the configured mail service with MailKit (ADR 0014): always encrypted,
/// authenticated when a user name is set, and given up after 30 seconds. It logs nothing, so the
/// credential and the address never reach a log or a trace (L2-084).
/// </summary>
public sealed class SmtpMailSender(IOptions<MailOptions> options) : IMailSender
{
    internal static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public bool IsConfigured => true;

    /// <summary>Port 465 is TLS from the first byte; any other port must offer STARTTLS.</summary>
    public static SecureSocketOptions SocketOptionsFor(int port) =>
        port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

    public async Task SendAsync(Application.Alerts.MailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var settings = options.Value;
        using var mime = new MimeMessage();
        mime.From.Add(MailboxAddress.Parse(settings.From ?? string.Empty));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new TextPart("plain") { Text = message.PlainTextBody };

        using var client = new SmtpClient { Timeout = (int)Timeout.TotalMilliseconds };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        await client.ConnectAsync(settings.Host ?? string.Empty, settings.Port, SocketOptionsFor(settings.Port), timeout.Token).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(settings.Username))
        {
            await client.AuthenticateAsync(settings.Username, settings.Password ?? string.Empty, timeout.Token).ConfigureAwait(false);
        }

        await client.SendAsync(new SendRequest(mime), timeout.Token).ConfigureAwait(false);
        await client.DisconnectAsync(quit: true, timeout.Token).ConfigureAwait(false);
    }
}
