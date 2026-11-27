using Keryx.Infrastructure.Persistence.Schema;

namespace Keryx.Api.Health;

/// <summary>
/// Tells the operator, once as the API starts, where the database's schema stands (L2-095). When
/// it is behind, the API still starts, reports not ready and logs that <c>migrate</c> must be
/// run. It never migrates.
/// </summary>
internal sealed class SchemaStatusLogger(IServiceScopeFactory scopes, ILogger<SchemaStatusLogger> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var state = await scope.ServiceProvider.GetRequiredService<ISchemaVersion>().GetAsync(stoppingToken).ConfigureAwait(false);
            SchemaStatusLog.Report(logger, state.Status);
        }
    }
}
