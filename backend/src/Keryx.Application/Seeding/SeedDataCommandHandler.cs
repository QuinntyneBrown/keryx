using Keryx.Application.Abstractions;
using Keryx.Domain.Seeding;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Seeding;

/// <summary>
/// Adds each seed row whose natural key is not there yet: a region and a category by slug, the
/// one settings row, a news query source by its seed key. A row that exists is never read back
/// or rewritten, so what staff changed since stays changed.
/// </summary>
internal sealed class SeedDataCommandHandler(IAppDbContext db, TimeProvider time) : IRequestHandler<SeedDataCommand, SeedDataResult>
{
    public async Task<SeedDataResult> Handle(SeedDataCommand request, CancellationToken cancellationToken)
    {
        var regionSlugs = await db.Regions.Select(region => region.Slug).ToListAsync(cancellationToken).ConfigureAwait(false);
        var regions = SeedData.Regions().Where(region => !regionSlugs.Contains(region.Slug)).ToList();
        db.Regions.AddRange(regions);

        var categorySlugs = await db.Categories.Select(category => category.Slug).ToListAsync(cancellationToken).ConfigureAwait(false);
        var categories = SeedData.Categories().Where(category => !categorySlugs.Contains(category.Slug)).ToList();
        db.Categories.AddRange(categories);

        var hasSettings = await db.Settings.AnyAsync(cancellationToken).ConfigureAwait(false);
        if (!hasSettings)
        {
            db.Settings.Add(SeedData.DefaultSettings());
        }

        var seedKeys = await db.Sources
            .Where(source => source.SeedKey != null)
            .Select(source => source.SeedKey)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var sources = SeedData.NewsQuerySources(time.GetUtcNow()).Where(source => !seedKeys.Contains(source.SeedKey)).ToList();
        db.Sources.AddRange(sources);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new SeedDataResult(regions.Count, categories.Count, hasSettings ? 0 : 1, sources.Count);
    }
}
