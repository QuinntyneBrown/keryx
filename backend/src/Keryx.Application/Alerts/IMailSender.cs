namespace Keryx.Application.Alerts;

/// <summary>One plain-text e-mail to one address (L2-077 AC6).</summary>
public sealed record MailMessage(string To, string Subject, string PlainTextBody);

/// <summary>
/// The mail service. Handlers send through it and never open a connection of their own
/// (L2-077 AC6); its client is in Infrastructure.
/// </summary>
public interface IMailSender
{
    /// <summary>A mail service is configured. Without one no e-mail can be delivered, so none is attempted.</summary>
    bool IsConfigured { get; }

    /// <summary>Sends the message, or throws when the mail service fails.</summary>
    Task SendAsync(MailMessage message, CancellationToken cancellationToken);
}

/// <summary>Where the admin app is served: the only place an alert's links lead (L2-077 AC6).</summary>
public sealed class AlertOptions
{
    public const string Section = "Keryx:AdminApp";

    public Uri? Origin { get; set; }
}
