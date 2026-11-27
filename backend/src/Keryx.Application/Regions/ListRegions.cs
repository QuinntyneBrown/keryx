using Keryx.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Regions;

/// <summary>A region as staff choose it: its key, its name and the zone its times are shown in.</summary>
public sealed record RegionDto(string Slug, string Name, string TimeZone);

/// <summary>The regions in rank order, Toronto first (L2-018). The list is fixed and small, and bounded at 200.</summary>
public sealed record ListRegions : IRequest<RegionList>;

public sealed record RegionList(IReadOnlyList<RegionDto> Items);

internal sealed class ListRegionsHandler(IAppDbContext db) : IRequestHandler<ListRegions, RegionList>
{
    public const int Bound = 200;

    public async Task<RegionList> Handle(ListRegions request, CancellationToken cancellationToken) =>
        new(await db.Regions.AsNoTracking()
            .OrderBy(region => region.Rank).ThenBy(region => region.Slug)
            .Take(Bound)
            .Select(region => new RegionDto(region.Slug, region.Name, region.TimeZone))
            .ToListAsync(cancellationToken).ConfigureAwait(false));
}
