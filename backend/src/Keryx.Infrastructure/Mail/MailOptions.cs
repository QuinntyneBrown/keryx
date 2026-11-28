namespace Keryx.Infrastructure.Mail;

/// <summary>The mail service alerts are sent through (tools/deploy/configuration.md).</summary>
public sealed class MailOptions
{
    public const string Section = "Keryx:Mail";

    public const string MissingFrom = "Missing required setting: Keryx__Mail__From";

    public const string MissingOrigin = "Missing required setting: Keryx__AdminApp__Origin";

    /// <summary>Unset: no mail service, and no e-mail is sent.</summary>
    public string? Host { get; set; }

    /// <summary>A mail host is set: alerts are sent, and a sender and the admin app's origin are required.</summary>
    public bool Configured => !string.IsNullOrWhiteSpace(Host);

    public int Port { get; set; } = 587;

    public string? Username { get; set; }

    /// <summary>A secret: never logged or traced (L2-084).</summary>
    public string? Password { get; set; }

    /// <summary>The address alerts are sent from.</summary>
    public string? From { get; set; }
}
