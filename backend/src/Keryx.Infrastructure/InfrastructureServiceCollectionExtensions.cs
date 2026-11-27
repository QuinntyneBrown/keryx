using Keryx.Application.Abstractions;
using Keryx.Infrastructure.Configuration;
using Keryx.Infrastructure.Health;
using Keryx.Infrastructure.Persistence;
using Keryx.Infrastructure.Persistence.Schema;
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
        services.AddOptions<DatabaseOptions>()
            .Configure(options => options.ConnectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionName));

        services.AddDbContext<KeryxDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString));
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<KeryxDbContext>());
        services.AddScoped<ISchemaVersion, EfSchemaVersion>();
        services.AddScoped<IDatabaseMigrator, EfDatabaseMigrator>();
        services.AddHealthChecks().AddCheck<DatabaseReadinessCheck>("database", tags: [HealthTags.Ready]);
        return services;
    }
}
