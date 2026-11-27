using System.Globalization;
using FluentValidation;
using Keryx.Application.Abstractions;
using Keryx.Domain.Catalogue;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Consumer;

/// <summary>
/// A page of served articles, newest first, matching every filter given (L2-026). Each value is
/// as the query string gave it, so a value of the wrong form is refused by the validator with its
/// own code rather than by model binding.
/// </summary>
public sealed record ListArticles(string? Limit, string? Cursor, string? Region, string? Category, string? Org, string? Since)
    : IRequest<PageDto<ArticleSummaryDto>>
{
    public const string ListName = "articles";

    /// <summary>The filters a cursor is bound to: a cursor read with other filters is refused.</summary>
    public string Filters =>
        string.Join('&', $"region={Region}", $"category={Category}", $"org={Org}",
            $"since={(FilterTime.TryParse(Since, out var since) ? since.UtcTicks.ToString(CultureInfo.InvariantCulture) : string.Empty)}");
}

/// <summary>A page of a list: its items and the cursor of the next page, or null on the last.</summary>
public sealed record PageDto<T>(IReadOnlyList<T> Data, string? NextCursor);

/// <summary>An article in a list: no tags, sources or internal field.</summary>
public sealed record ArticleSummaryDto(
    string Id, string Title, OrganizationRefDto? Organization, string Category, string Region, DateTimeOffset? PublishedAt, string Url, string Summary);

/// <summary>Each value refused with the code a product branches on: <c>invalid_limit</c>, <c>invalid_filter</c> or <c>invalid_cursor</c>.</summary>
internal sealed class ListArticlesValidator : AbstractValidator<ListArticles>
{
    public ListArticlesValidator(IAppDbContext db, ICursorCodec cursors)
    {
        RuleFor(request => request.Limit)
            .Must(limit => limit is null || PageLimit.TryParse(limit, out _))
            .WithErrorCode("invalid_limit")
            .WithMessage("Give limit as a whole number from 1 to 100.");
        RuleFor(request => request.Region).MustBeKnownRegion(db);
        RuleFor(request => request.Category)
            .Must(category => category is null || ArticleCategory.Slugs.Contains(category))
            .WithErrorCode("invalid_filter")
            .WithMessage("Give category as a slug that /v1/categories lists.");
        RuleFor(request => request.Org).MustBeOrganizationId();
        RuleFor(request => request.Since)
            .Must(since => since is null || FilterTime.TryParse(since, out _))
            .WithErrorCode("invalid_filter")
            .WithMessage("Give since as an ISO 8601 time with its offset, such as 2026-11-26T12:00:00Z.");
        RuleFor(request => request.Cursor)
            .Must((request, cursor) => cursor is null
                || cursors.Decode(cursor) is { List: ListArticles.ListName, Time: not null } position && position.Filters == request.Filters)
            .WithErrorCode("invalid_cursor")
            .WithMessage("Use the next_cursor of a page read with the same filters.");
    }
}

/// <summary>How many items a page holds: 20 unless a product asks for 1 to 100.</summary>
public static class PageLimit
{
    public const int Default = 20;
    public const int Max = 100;

    public static bool TryParse(string text, out int limit) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out limit) && limit is >= 1 and <= Max;
}

/// <summary>A time a product filters by (<c>since</c>, <c>from</c>, <c>to</c>): ISO 8601 with a date, a time and an offset (<c>Z</c> or <c>±hh:mm</c>).</summary>
public static class FilterTime
{
    private static readonly string[] Formats = ["yyyy-MM-dd'T'HH:mm:ssK", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"];

    public static bool TryParse(string? text, out DateTimeOffset time)
    {
        time = default;
        return text is not null
            && (text.EndsWith('Z') || text.Length > 6 && text[^6] is '+' or '-')
            && DateTimeOffset.TryParseExact(text, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
    }
}

/// <summary>
/// One keyset query: served articles matching every filter, ordered by (ListedAt, Id) descending,
/// sought after the cursor's position, taking one row more than the limit to know whether a next
/// page exists. An article published after the first page sorts before its position, so a later
/// page neither repeats nor skips what was there.
/// </summary>
internal sealed class ListArticlesHandler(IAppDbContext db, ICursorCodec cursors) : IRequestHandler<ListArticles, PageDto<ArticleSummaryDto>>
{
    public async Task<PageDto<ArticleSummaryDto>> Handle(ListArticles request, CancellationToken cancellationToken)
    {
        var limit = request.Limit is null ? PageLimit.Default : int.Parse(request.Limit, CultureInfo.InvariantCulture);
        var query = ServedRecordFilters.ServedArticles(db.Articles).AsNoTracking();
        if (request.Region is { } region)
        {
            query = query.Where(article => article.RegionSlug == region);
        }

        if (request.Category is { } category)
        {
            query = query.Where(article => article.Category == category);
        }

        if (request.Org is { } org)
        {
            query = query.Where(article => article.Organization != null && article.Organization.PublicId == org);
        }

        if (FilterTime.TryParse(request.Since, out var since))
        {
            query = query.Where(article => article.ListedAt >= since);
        }

        if (request.Cursor is not null && cursors.Decode(request.Cursor) is { Time: { } afterTime } after)
        {
            query = query.Where(article => article.ListedAt < afterTime || (article.ListedAt == afterTime && article.Id < after.Id));
        }

        var rows = await query
            .OrderByDescending(article => article.ListedAt).ThenByDescending(article => article.Id)
            .Take(limit + 1)
            .Select(article => new
            {
                article.Id,
                article.ListedAt,
                Item = new ArticleSummaryDto(
                    article.PublicId,
                    article.Title!,
                    article.Organization == null ? null : new OrganizationRefDto(article.Organization.PublicId, article.Organization.Name),
                    article.Category,
                    article.RegionSlug,
                    article.PublishedAt,
                    article.Url,
                    article.Summary),
            })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (rows.Count <= limit)
        {
            return new PageDto<ArticleSummaryDto>([.. rows.Select(row => row.Item)], null);
        }

        var last = rows[limit - 1];
        return new PageDto<ArticleSummaryDto>(
            [.. rows.Take(limit).Select(row => row.Item)],
            cursors.Encode(CursorPosition.At(ListArticles.ListName, request.Filters, last.ListedAt, last.Id)));
    }
}
