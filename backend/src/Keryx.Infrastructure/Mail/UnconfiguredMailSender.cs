using Keryx.Application.Alerts;

namespace Keryx.Infrastructure.Mail;

/// <summary>No mail service is configured: no alert can be delivered, so none is attempted.</summary>
internal sealed class UnconfiguredMailSender : IMailSender
{
    public bool IsConfigured => false;

    public Task SendAsync(MailMessage message, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("No mail service is configured.");
}
