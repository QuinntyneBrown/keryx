using System.Text.Json.Serialization;
using Keryx.Application.Abstractions;
using Keryx.Domain.Clients;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Keryx.Application.Clients;

/// <summary>The consumer API's public address, which staff give to the products that call it.</summary>
public sealed class ConsumerApiOptions
{
    public const string Section = "Keryx:Api";

    public const string MissingPublicBaseUrl = "Missing required setting: Keryx__Api__PublicBaseUrl";

    /// <summary>An absolute http(s) address ending in /v1.</summary>
    public Uri? PublicBaseUrl { get; set; }

    public static bool IsValid(Uri? address) =>
        address is { IsAbsoluteUri: true }
        && (address.Scheme == Uri.UriSchemeHttps || address.Scheme == Uri.UriSchemeHttp)
        && address.AbsolutePath.TrimEnd('/').EndsWith("/v1", StringComparison.Ordinal);
}

/// <summary>The whole API's usage over the last 24 hours, for the API screen (L2-037 AC3).</summary>
public sealed record GetApiUsage : IRequest<ApiUsageDto>;

/// <summary>Every count a number: no requests reads 0 requests, a null p95 and a rate of 0.</summary>
public sealed record ApiUsageDto(
    Uri BaseUrl,
    [property: JsonPropertyName("requests_24h")] int Requests24h,
    [property: JsonPropertyName("p95_latency_ms")] int? P95LatencyMs,
    double ErrorRate);

/// <summary>
/// Sums every client's hours in the window. The latency counts are held as JSON, so the rows are
/// read and merged here: at most 24 per client.
/// </summary>
internal sealed class GetApiUsageHandler(IAppDbContext db, TimeProvider time, IOptions<ConsumerApiOptions> options) : IRequestHandler<GetApiUsage, ApiUsageDto>
{
    public async Task<ApiUsageDto> Handle(GetApiUsage request, CancellationToken cancellationToken)
    {
        var since = ApiUsageHour.WindowStart(time.GetUtcNow());
        var hours = await db.ApiUsageHours.AsNoTracking().Where(hour => hour.HourStart >= since)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var requests = hours.Sum(hour => hour.Requests);
        var errors = hours.Sum(hour => hour.Errors);
        var buckets = new int[LatencyBuckets.Count];
        foreach (var hour in hours)
        {
            for (var index = 0; index < buckets.Length; index++)
            {
                buckets[index] += hour.LatencyBucketCounts[index];
            }
        }

        return new ApiUsageDto(options.Value.PublicBaseUrl!, requests, ApiUsageSummary.P95LatencyMs(buckets), ApiUsageSummary.ErrorRate(requests, errors));
    }
}
