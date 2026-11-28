using Keryx.Application.Abstractions;
using Keryx.Domain.Clients;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Clients;

/// <summary>
/// One request through the consumer API, by the client the key named: its status, how long it
/// took and when it ended. No path, query, address, header or body is passed (L2-037).
/// </summary>
public sealed record RecordApiUsage(string ClientId, int Status, TimeSpan Elapsed, DateTimeOffset At) : IRequest;

/// <summary>Moves the usage counted in memory into each client's hourly rows (L2-037).</summary>
public sealed record FlushApiUsage : IRequest;

/// <summary>One client's counts for one hour, gathered since the last flush.</summary>
public sealed record ApiUsageTally(string ClientId, DateTimeOffset HourStart, int Requests, int Errors, IReadOnlyList<int> LatencyBucketCounts, DateTimeOffset LastCallAt);

/// <summary>Counts requests in memory, so a request writes nothing to the database.</summary>
public interface IApiUsageAccumulator
{
    void Add(string clientId, DateTimeOffset at, bool failed, int latencyBucket);

    /// <summary>Takes everything counted so far, leaving nothing behind.</summary>
    IReadOnlyList<ApiUsageTally> Drain();

    /// <summary>Puts back tallies a flush could not save, to join the next flush.</summary>
    void Restore(IReadOnlyList<ApiUsageTally> tallies);
}

internal sealed class RecordApiUsageHandler(IApiUsageAccumulator usage) : IRequestHandler<RecordApiUsage>
{
    public Task Handle(RecordApiUsage request, CancellationToken cancellationToken)
    {
        usage.Add(request.ClientId, request.At, ApiUsageClassifier.IsError(request.Status), LatencyBuckets.IndexOf(request.Elapsed));
        return Task.CompletedTask;
    }
}

/// <summary>
/// Adds each tally to its client's row for that hour, creating it when absent, and moves the
/// client's last call forward, in one unit of work. A client deleted meanwhile is skipped. A save
/// that fails puts the tallies back, so they join the next flush.
/// </summary>
internal sealed class FlushApiUsageHandler(IAppDbContext db, IApiUsageAccumulator usage) : IRequestHandler<FlushApiUsage>
{
    public async Task Handle(FlushApiUsage request, CancellationToken cancellationToken)
    {
        var tallies = usage.Drain();
        if (tallies.Count == 0)
        {
            return;
        }

        // Everything after the drain is inside the try: a read, a save or a cancellation that
        // fails puts the counts back for the next flush, so none is lost (L2-037).
        try
        {
            await SaveAsync(tallies, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            usage.Restore(tallies);
            throw;
        }
    }

    private async Task SaveAsync(IReadOnlyList<ApiUsageTally> tallies, CancellationToken cancellationToken)
    {
        var publicIds = tallies.Select(tally => tally.ClientId).Distinct().ToList();
        var clients = await db.ApiClients.Where(client => publicIds.Contains(client.PublicId))
            .ToDictionaryAsync(client => client.PublicId, cancellationToken).ConfigureAwait(false);
        var ids = clients.Values.Select(client => client.Id).ToList();
        var hours = tallies.Select(tally => tally.HourStart).Distinct().ToList();
        var rows = await db.ApiUsageHours.Where(row => ids.Contains(row.ClientId) && hours.Contains(row.HourStart))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var tally in tallies)
        {
            if (!clients.TryGetValue(tally.ClientId, out var client))
            {
                continue;
            }

            var row = rows.SingleOrDefault(candidate => candidate.ClientId == client.Id && candidate.HourStart == tally.HourStart);
            if (row is null)
            {
                row = ApiUsageHour.Start(client.Id, tally.HourStart);
                rows.Add(row);
                db.ApiUsageHours.Add(row);
            }

            row.Add(tally.Requests, tally.Errors, tally.LatencyBucketCounts);
            client.Called(tally.LastCallAt);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
