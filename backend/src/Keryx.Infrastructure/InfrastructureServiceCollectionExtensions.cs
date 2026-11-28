using Keryx.Application.Abstractions;
using Keryx.Application.Sources;
using Keryx.Infrastructure.Configuration;
using Keryx.Infrastructure.Health;
using Keryx.Infrastructure.Persistence;
using Keryx.Infrastructure.Persistence.Schema;
using Keryx.Infrastructure.Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Keryx.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// The database, the schema-version check and the readiness health check, for the API, the
    /// worker and the CLI alike.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        // A required setting that is missing stops the process as it starts, by name (L2-096).
        services.AddOptions<DatabaseOptions>()
            .Configure(options => options.ConnectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), DatabaseOptions.MissingConnectionString)
            .ValidateOnStart();

        services.AddDbContext<KeryxDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString));
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<KeryxDbContext>());
        services.AddScoped<ISchemaVersion, EfSchemaVersion>();
        services.AddSingleton<IHostResolver, DnsHostResolver>();
        services.AddSingleton<IFeedParser, FeedParser>();
        services.AddSingleton<Keryx.Application.Clients.IApiKeyGenerator, Clients.ApiKeyGenerator>();
        services.AddSingleton<Keryx.Application.Clients.IRateWindowStore, Clients.InMemoryRateWindowStore>();
        services.AddSingleton<Keryx.Application.Clients.IApiUsageAccumulator, Clients.InMemoryApiUsageAccumulator>();
        services.AddScoped<IDatabaseMigrator, EfDatabaseMigrator>();
        services.AddHealthChecks().AddCheck<DatabaseReadinessCheck>("database", tags: [HealthTags.Ready]);
        return services;
    }

    /// <summary>
    /// The mail service that alerts are sent through, for the worker, and the admin app's origin
    /// their links lead to (L2-077). With no mail host none is sent, and the worker says so once;
    /// with one, a sender address and the origin are required, and a worker without them does not
    /// start (L2-096).
    /// </summary>
    public static IServiceCollection AddMail(this IServiceCollection services)
    {
        // The origin is checked with the mail settings: it is required only when mail is sent.
        services.AddOptions<Keryx.Application.Alerts.AlertOptions>().BindConfiguration(Keryx.Application.Alerts.AlertOptions.Section);
        services.AddOptions<Mail.MailOptions>().BindConfiguration(Mail.MailOptions.Section)
            .Validate(options => !options.Configured || MimeKit.MailboxAddress.TryParse(options.From, out _), Mail.MailOptions.MissingFrom)
            .Validate<IOptions<Keryx.Application.Alerts.AlertOptions>>(
                (mail, alerts) => !mail.Configured
                    || alerts.Value.Origin is { IsAbsoluteUri: true } origin && (origin.Scheme == Uri.UriSchemeHttps || origin.Scheme == Uri.UriSchemeHttp),
                Mail.MailOptions.MissingOrigin)
            .ValidateOnStart();
        services.AddSingleton<Keryx.Application.Alerts.IMailSender>(provider =>
            provider.GetRequiredService<IOptions<Mail.MailOptions>>().Value.Configured
                ? ActivatorUtilities.CreateInstance<Mail.SmtpMailSender>(provider)
                : new Mail.UnconfiguredMailSender());
        services.AddHostedService<Mail.MailConfigurationNotice>();
        return services;
    }

    /// <summary>
    /// The source fetcher, for the processes that fetch sources. It needs the contact URL its
    /// User-Agent names, and a process without it does not start (L2-021 AC3, L2-096).
    /// </summary>
    public static IServiceCollection AddSourceFetching(this IServiceCollection services)
    {
        services.AddOptions<FetchOptions>().BindConfiguration(FetchOptions.Section)
            .Validate(options => options.UserAgentContactUrl is { IsAbsoluteUri: true } url && url.Scheme == Uri.UriSchemeHttps, FetchOptions.MissingContactUrl)
            .ValidateOnStart();
        services.AddSingleton(provider => SourceSockets.Connect(provider.GetRequiredService<IHostResolver>()));
        services.AddSingleton<ISourceFetcher, HttpSourceFetcher>();
        return services;
    }
}
