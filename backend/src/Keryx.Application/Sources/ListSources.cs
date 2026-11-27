using FluentValidation;
using Keryx.Application.Abstractions;
using Keryx.Application.Errors;
using Keryx.Domain.Sources;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Sources;

/// <summary>One page of the source catalogue, failing first, optionally of one kind (L2-062).</summary>
public sealed record ListSources(string? Kind, int Page, int PageSize) : IRequest<SourcePage>;

/// <summary>
/// A page of sources, with counts across the whole catalogue whatever the filter, so the header
/// reads the same on every chip. Every count is a number.
/// </summary>
public sealed record SourcePage(
    IReadOnlyList<SourceListItem> Items,
    int Total,
    int Page,
    int PageSize,
    int AllCount,
    int FailingCount,
    int NeedsLookCount);

/// <summary>
/// A source as the list shows it. The held count and the 30-day yield are filled by the runs that
/// collect articles (journeys 4 to 6); until a run records them they are 0, and the failure
/// reason is null.
/// </summary>
public sealed record SourceListItem(
    string Id,
    string Kind,
    string Address,
    string OrganizationName,
    string Region,
    string RegionName,
    string Health,
    string? FailureReason,
    int ConsecutiveFailures,
    int HeldCount,
    [property: System.Text.Json.Serialization.JsonPropertyName("articles_30d")] int Articles30d,
    DateTimeOffset? LastFetchedAt);

internal sealed class ListSourcesValidator : AbstractValidator<ListSources>
{
    public ListSourcesValidator() =>
        RuleFor(request => request.Kind)
            .Must(kind => kind is null || SourceWords.KindOf(kind) is not null)
            .WithMessage("Choose a kind: rss, website, instagram, eventbrite or news_query.");
}

internal sealed class ListSourcesHandler(IAppDbContext db) : IRequestHandler<ListSources, SourcePage>
{
    public async Task<SourcePage> Handle(ListSources request, CancellationToken cancellationToken)
    {
        InvalidPageException.ThrowIfOutOfRange(request.Page, request.PageSize);
        var all = db.Sources.AsNoTracking();
        var counts = await all
            .GroupBy(source => source.Health)
            .Select(group => new { Health = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var filtered = SourceWords.KindOf(request.Kind) is { } kind ? all.Where(source => source.Kind == kind) : all;
        var total = await filtered.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await filtered
            .OrderBy(source => source.Health == SourceHealth.Failing ? 0 : source.Health == SourceHealth.NeedsLook ? 1 : 2)
            .ThenBy(source => source.OrganizationName)
            .ThenBy(source => source.PublicId)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Join(db.Regions, source => source.RegionSlug, region => region.Slug, (source, region) => new { source, region.Name })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return new SourcePage(
            [.. rows.Select(row => new SourceListItem(
                row.source.PublicId,
                SourceWords.Kind(row.source.Kind),
                row.source.Address,
                row.source.OrganizationName,
                row.source.RegionSlug,
                row.Name,
                SourceWords.Health(row.source.Health),
                null,
                row.source.ConsecutiveFailures,
                0,
                0,
                row.source.LastFetchedAt))],
            total,
            request.Page,
            request.PageSize,
            counts.Sum(count => count.Count),
            counts.Where(count => count.Health == SourceHealth.Failing).Sum(count => count.Count),
            counts.Where(count => count.Health == SourceHealth.NeedsLook).Sum(count => count.Count));
    }
}
