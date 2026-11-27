using Keryx.Application.Abstractions;
using Keryx.Application.Runs;
using Keryx.Domain;
using Keryx.Domain.Collection;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Consumer;

/// <summary>A short, fixed list: its items under <c>data</c>, as every /v1 list reads.</summary>
public sealed record ListDto<T>(IReadOnlyList<T> Data);

/// <summary>A category and its count of published articles, a number (L2-030 AC1).</summary>
public sealed record CategoryDto(string Slug, string Name, int ArticleCount);

/// <summary>A region, the zone to show its times in, and its count of published articles (L2-030 AC2).</summary>
public sealed record RegionDto(string Slug, string Name, string TimeZone, int ArticleCount);

/// <summary>
/// How fresh collection is (L2-030 AC3): the latest collection run's start and outcome, both null
/// before any run, the next scheduled time and the published total. It names no source, issue or error.
/// </summary>
public sealed record StatusDto(DateTimeOffset? LastRunAt, string? Status, DateTimeOffset NextRunAt, int ArticlesTotal);

/// <summary>The six categories in the order of L2-015.</summary>
public sealed record ListCategories : IRequest<ListDto<CategoryDto>>;

internal sealed class ListCategoriesHandler(IAppDbContext db) : IRequestHandler<ListCategories, ListDto<CategoryDto>>
{
    public async Task<ListDto<CategoryDto>> Handle(ListCategories request, CancellationToken cancellationToken)
    {
        var served = ServedRecordFilters.ServedArticles(db.Articles);
        var categories = await db.Categories.AsNoTracking()
            .OrderBy(category => category.Rank)
            .Select(category => new CategoryDto(category.Slug, category.Name, served.Count(article => article.Category == category.Slug)))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return new ListDto<CategoryDto>(categories);
    }
}

/// <summary>The regions by rank, Toronto first and global last (L2-018), at most 100.</summary>
public sealed record ListRegions : IRequest<ListDto<RegionDto>>
{
    public const int Max = 100;
}

internal sealed class ListRegionsHandler(IAppDbContext db) : IRequestHandler<ListRegions, ListDto<RegionDto>>
{
    public async Task<ListDto<RegionDto>> Handle(ListRegions request, CancellationToken cancellationToken)
    {
        var served = ServedRecordFilters.ServedArticles(db.Articles);
        var regions = await db.Regions.AsNoTracking()
            .OrderBy(region => region.Rank).ThenBy(region => region.Slug)
            .Take(ListRegions.Max)
            .Select(region => new RegionDto(region.Slug, region.Name, region.TimeZone, served.Count(article => article.RegionSlug == region.Slug)))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return new ListDto<RegionDto>(regions);
    }
}

/// <summary>The status of collection as a whole.</summary>
public sealed record GetStatus : IRequest<StatusDto>;

internal sealed class GetStatusHandler(IAppDbContext db, TimeProvider time) : IRequestHandler<GetStatus, StatusDto>
{
    public async Task<StatusDto> Handle(GetStatus request, CancellationToken cancellationToken)
    {
        var latest = await db.Runs.AsNoTracking()
            .Where(run => run.Worker == RunWorker.Collection)
            .OrderByDescending(run => run.StartedAt).ThenByDescending(run => run.Id)
            .Select(run => new { run.StartedAt, run.Outcome })
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var settings = await db.Settings.AsNoTracking().SingleAsync(cancellationToken).ConfigureAwait(false);
        var next = CollectionSchedule.NextSlotAfter(time.GetUtcNow(), ScheduleInterval.From(settings.IntervalHours), TorontoTime.Zone);
        var total = await ServedRecordFilters.ServedArticles(db.Articles).CountAsync(cancellationToken).ConfigureAwait(false);
        return new StatusDto(latest?.StartedAt, latest is null ? null : RunWords.Outcome(latest.Outcome), next.ToUniversalTime(), total);
    }
}
