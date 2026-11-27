using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FluentValidation;
using Keryx.Application.Abstractions;
using Keryx.Domain.Catalogue;
using Keryx.Domain.Collection;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Catalogue;

/// <summary>A row of the staff list (L2-048): the text came from a source, so a client renders it as text.</summary>
public sealed record AdminArticleRowDto(
    string Id, string? Title, string? Organization, string Category, string Region, decimal Relevance, string Status,
    DateTimeOffset? PublishedAt, DateTimeOffset CollectedAt);

/// <summary>A page of the staff list, with the total that matched, all as numbers.</summary>
public sealed record AdminArticlePageDto(IReadOnlyList<AdminArticleRowDto> Items, int Total, int Page, int PageSize);

/// <summary>
/// A page of every collected article, newest collected first, narrowed by status, a search, a
/// region and a category together (L2-048, L2-050). Values are as the query string gave them.
/// </summary>
public sealed record ListAdminArticles(string? Page, string? PageSize, string? Status, string? Q, string? Region, string? Category)
    : IRequest<AdminArticlePageDto>
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;
    public const int MaxSearch = 100;

    /// <summary>The status words a client filters by, as they are served.</summary>
    public static readonly IReadOnlyList<string> Statuses = ["published", "needs_review", "hidden"];

    public static PublicationStatus StatusOf(string word) => word switch
    {
        "published" => PublicationStatus.Published,
        "needs_review" => PublicationStatus.NeedsReview,
        _ => PublicationStatus.Hidden,
    };

    public static bool TryParseNumber(string? text, int min, int max, out int value) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= min && value <= max;
}

internal sealed class ListAdminArticlesValidator : AbstractValidator<ListAdminArticles>
{
    public ListAdminArticlesValidator(IAppDbContext db)
    {
        RuleFor(request => request.PageSize)
            .Must(size => size is null || ListAdminArticles.TryParseNumber(size, 1, ListAdminArticles.MaxPageSize, out _))
            .WithErrorCode("invalid_page")
            .WithMessage("Give page_size as a whole number from 1 to 100.");
        RuleFor(request => request.Page)
            .Must(page => page is null || ListAdminArticles.TryParseNumber(page, 1, int.MaxValue, out _))
            .WithErrorCode("invalid_page")
            .WithMessage("Give page as a whole number from 1.");
        RuleFor(request => request.Status)
            .Must(status => status is null || ListAdminArticles.Statuses.Contains(status))
            .WithErrorCode("invalid_filter")
            .WithMessage("Give status as published, needs_review or hidden.");
        RuleFor(request => request.Region)
            .MustAsync(async (region, cancellationToken) => region is null
                || await db.Regions.AnyAsync(known => known.Slug == region, cancellationToken).ConfigureAwait(false))
            .WithErrorCode("invalid_filter")
            .WithMessage("Give region as a region's slug.");
        RuleFor(request => request.Category)
            .Must(category => category is null || ArticleCategory.Slugs.Contains(category))
            .WithErrorCode("invalid_filter")
            .WithMessage("Give category as a category's slug.");
    }
}

/// <summary>
/// Filters, counts the total and reads one page, newest collected first then newest id. The search
/// uses the first 100 characters and matches title, organization name, region name and the place
/// of the article's event as literal text without regard to case: EF sends the term as a
/// parameter of a containment test, never as a LIKE pattern, so <c>%</c>, <c>_</c> and quotes are
/// only themselves.
/// </summary>
internal sealed class ListAdminArticlesHandler(IAppDbContext db) : IRequestHandler<ListAdminArticles, AdminArticlePageDto>
{
    [SuppressMessage("Globalization", "CA1304", Justification = "Inside an EF query, ToUpper() is SQL upper(), not a .NET call.")]
    [SuppressMessage("Globalization", "CA1311", Justification = "Inside an EF query, ToUpper() is SQL upper(), not a .NET call.")]
    [SuppressMessage("Performance", "CA1862", Justification = "Inside an EF query, Contains(string) is SQL containment; a StringComparison overload does not translate.")]
    public async Task<AdminArticlePageDto> Handle(ListAdminArticles request, CancellationToken cancellationToken)
    {
        var page = request.Page is null ? 1 : int.Parse(request.Page, CultureInfo.InvariantCulture);
        var size = request.PageSize is null ? ListAdminArticles.DefaultPageSize : int.Parse(request.PageSize, CultureInfo.InvariantCulture);
        var query = db.Articles.AsNoTracking();
        if (request.Status is { } word)
        {
            var status = ListAdminArticles.StatusOf(word);
            query = query.Where(article => article.Status == status);
        }

        if (request.Region is { } region)
        {
            query = query.Where(article => article.RegionSlug == region);
        }

        if (request.Category is { } category)
        {
            query = query.Where(article => article.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            var term = (request.Q.Length > ListAdminArticles.MaxSearch ? request.Q[..ListAdminArticles.MaxSearch] : request.Q).ToUpperInvariant();
            var regions = db.Regions;
            var events = db.Events;
            query = query.Where(article =>
                (article.Title != null && article.Title.ToUpper().Contains(term))
                || (article.Organization != null && article.Organization.Name.ToUpper().Contains(term))
                || regions.Any(known => known.Slug == article.RegionSlug && known.Name.ToUpper().Contains(term))
                || events.Any(item => item.LinkedArticleId == article.Id && item.PlaceName.ToUpper().Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(article => article.CollectedAt).ThenByDescending(article => article.Id)
            .Skip((page - 1) * size).Take(size)
            .Select(article => new
            {
                article.PublicId,
                article.Title,
                Organization = article.Organization == null ? null : article.Organization.Name,
                article.Category,
                article.RegionSlug,
                article.Score,
                article.Status,
                article.PublishedAt,
                article.CollectedAt,
            })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return new AdminArticlePageDto(
            [.. items.Select(row => new AdminArticleRowDto(
                row.PublicId, row.Title, row.Organization, row.Category, row.RegionSlug, row.Score,
                GetAdminArticleHandler.StatusWord(row.Status), row.PublishedAt, row.CollectedAt))],
            total, page, size);
    }
}

/// <summary>The whole totals of each status (L2-049): the tabs show them whatever the search.</summary>
public sealed record GetArticleStatusCounts : IRequest<ArticleStatusCountsDto>;

public sealed record ArticleStatusCountsDto(int All, int NeedsReview, int Published, int Hidden);

internal sealed class GetArticleStatusCountsHandler(IAppDbContext db) : IRequestHandler<GetArticleStatusCounts, ArticleStatusCountsDto>
{
    public async Task<ArticleStatusCountsDto> Handle(GetArticleStatusCounts request, CancellationToken cancellationToken)
    {
        var counts = await db.Articles.AsNoTracking()
            .GroupBy(article => article.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        int Of(PublicationStatus status) => counts.Where(row => row.Status == status).Sum(row => row.Count);
        return new ArticleStatusCountsDto(counts.Sum(row => row.Count), Of(PublicationStatus.NeedsReview), Of(PublicationStatus.Published), Of(PublicationStatus.Hidden));
    }
}
