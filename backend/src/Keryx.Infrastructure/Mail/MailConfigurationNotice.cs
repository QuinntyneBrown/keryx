using Keryx.Application.Alerts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Keryx.Infrastructure.Mail;

/// <summary>Says once, as the worker starts, that no e-mail will be sent without a mail host.</summary>
internal sealed partial class MailConfigurationNotice(IMailSender mail, ILogger<MailConfigurationNotice> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!mail.IsConfigured)
        {
            NoMailService(logger);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Warning, Message = "No mail service is configured (Keryx__Mail__Host): alert e-mails and the daily digest are not sent.")]
    private static partial void NoMailService(ILogger logger);
}
