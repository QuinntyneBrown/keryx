using Keryx.Application.Abstractions;
using Keryx.Domain.Clients;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Clients;

/// <summary>Clients as staff read them, each with its usage over the last 24 hours (L2-037).</summary>
internal static class ClientUsage
{
    public static async Task<IReadOnlyList<ApiClientDto>> ReadAsync(IAppDbContext db, IReadOnlyList<ApiClient> clients, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var ids = clients.Select(client => client.Id).ToList();
        var since = ApiUsageHour.WindowStart(now);
        var usage = await db.ApiUsageHours.AsNoTracking()
            .Where(hour => ids.Contains(hour.ClientId) && hour.HourStart >= since)
            .GroupBy(hour => hour.ClientId)
            .Select(group => new { ClientId = group.Key, Requests = group.Sum(hour => hour.Requests), Errors = group.Sum(hour => hour.Errors) })
            .ToDictionaryAsync(row => row.ClientId, cancellationToken).ConfigureAwait(false);
        return [.. clients.Select(client => usage.TryGetValue(client.Id, out var used)
            ? ApiClientDto.From(client, used.Requests, used.Errors)
            : ApiClientDto.From(client))];
    }
}
