using Keryx.Infrastructure.Persistence.Schema;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Keryx.Infrastructure.Health;

/// <summary>
/// Readiness (L2-095): healthy only when the database is reachable and at the schema version
/// this build expects. It reports the status and nothing else: no version, host or error.
/// </summary>
internal sealed class DatabaseReadinessCheck(ISchemaVersion schema) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var state = await schema.GetAsync(cancellationToken).ConfigureAwait(false);
        return state.Ready ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
    }
}

/// <summary>The tags a health check is chosen by.</summary>
public static class HealthTags
{
    /// <summary>A check that readiness runs. Liveness runs none.</summary>
    public const string Ready = "ready";
}
