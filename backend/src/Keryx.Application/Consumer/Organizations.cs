using System.Globalization;
using FluentValidation;
using Keryx.Application.Abstractions;
using Keryx.Application.Errors;
using Keryx.Domain.Catalogue;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Consumer;

/// <summary>A listed organization as products read it (L2-029 AC1): its served article count is a number.</summary>
public sealed record OrganizationDto(string Id, string Name, string Kind, string Region, string? WebsiteUrl, int ArticleCount);

/// <summary>A page of listed organizations by name, matching every filter given (L2-029).</summary>
public sealed record ListOrganizations(string? Limit, string? Cursor, string? Region, string? Kind, string? Q) : IRequest<PageDto<OrganizationDto>>
{
    public const string ListName = "organizations";
    public const int QMin = 2;
    public const int QMax = 60;

    /// <summary>The kinds a product filters by, as they are served.</summary>
    public static readonly IReadOnlyList<string> Kinds = [.. Enum.GetValues<OrganizationKind>().Select(OrganizationKinds.Word)];

    public string Filters => string.Join('&', $"region={Region}", $"kind={Kind}", $"q={(Q is null ? null : OrganizationName.Normalize(Q))}");
}

internal sealed class ListOrganizationsValidator : AbstractValidator<ListOrganizations>
{
    public ListOrganizationsValidator(IAppDbContext db, ICursorCodec cursors)
    {
        RuleFor(request => request.Limit)
            .Must(limit => limit is null || PageLimit.TryParse(limit, out _))
            .WithErrorCode("invalid_limit")
            .WithMessage("Give limit as a whole number from 1 to 100.");
        RuleFor(request => request.Region).MustBeKnownRegion(db);
        RuleFor(request => request.Kind)
            .Must(kind => kind is null || ListOrganizations.Kinds.Contains(kind))
            .WithErrorCode(FilterRules.InvalidFilter)
            .WithMessage("Give kind as one of church, ministry, gym, club, network, business or other.");
        RuleFor(request => request.Q)
            .Must(q => q is null || q.Length is >= ListOrganizations.QMin and <= ListOrganizations.QMax)
            .WithErrorCode(FilterRules.InvalidFilter)
            .WithMessage("Give q as the start of a name, 2 to 60 characters.");
        RuleFor(request => request.Cursor)
            .Must((request, cursor) => cursor is null
                || cursors.Decode(cursor) is { List: ListOrganizations.ListName } position && position.Filters == request.Filters)
            .WithErrorCode("invalid_cursor")
            .WithMessage("Use the next_cursor of a page read with the same filters.");
    }
}

/// <summary>
/// One keyset query over listed organizations ordered by (Name, Id), each with its served article
/// count from a subquery in the same statement, taking one row more than the limit.
/// </summary>
internal sealed class ListOrganizationsHandler(IAppDbContext db, ICursorCodec cursors) : IRequestHandler<ListOrganizations, PageDto<OrganizationDto>>
{
    public async Task<PageDto<OrganizationDto>> Handle(ListOrganizations request, CancellationToken cancellationToken)
    {
        var limit = request.Limit is null ? PageLimit.Default : int.Parse(request.Limit, CultureInfo.InvariantCulture);
        var query = ServedRecordFilters.ListedOrganizations(db.Organizations, db.Articles, db.Events).AsNoTracking();
        if (request.Region is { } region)
        {
            query = query.Where(organization => organization.RegionSlug == region);
        }

        if (request.Kind is { } word)
        {
            var kind = OrganizationKinds.Of(word);
            query = query.Where(organization => organization.Kind == kind);
        }

        if (request.Q is { } q)
        {
            var prefix = OrganizationName.Normalize(q);
            query = query.Where(organization => organization.NormalizedName.StartsWith(prefix));
        }

        if (request.Cursor is not null && cursors.Decode(request.Cursor) is { } after)
        {
            // The database's collation orders the names and compares them, so the seek agrees with the sort.
            query = query.Where(organization => organization.Name.CompareTo(after.Sort) > 0
                || (organization.Name == after.Sort && organization.Id > after.Id));
        }

        var rows = await OrganizationProjection.Rows(query.OrderBy(organization => organization.Name).ThenBy(organization => organization.Id).Take(limit + 1), db.Articles)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (rows.Count <= limit)
        {
            return new PageDto<OrganizationDto>([.. rows.Select(row => row.Item)], null);
        }

        var last = rows[limit - 1];
        return new PageDto<OrganizationDto>(
            [.. rows.Take(limit).Select(row => row.Item)],
            cursors.Encode(new CursorPosition(ListOrganizations.ListName, request.Filters, last.Name, last.Id)));
    }
}

/// <summary>One listed organization by its public id (L2-029 AC3).</summary>
public sealed record GetOrganization(string Id) : IRequest<OrganizationDto>;

internal sealed class GetOrganizationHandler(IAppDbContext db) : IRequestHandler<GetOrganization, OrganizationDto>
{
    public async Task<OrganizationDto> Handle(GetOrganization request, CancellationToken cancellationToken)
    {
        var listed = ServedRecordFilters.ListedOrganizations(db.Organizations, db.Articles, db.Events).AsNoTracking()
            .Where(organization => organization.PublicId == request.Id);
        var row = await OrganizationProjection.Rows(listed, db.Articles).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return row?.Item ?? throw new OrganizationNotFoundException();
    }
}

public sealed class OrganizationNotFoundException() : KeryxException(404, "organization_not_found", "The organization was not found.");

/// <summary>The one projection both reads share.</summary>
internal static class OrganizationProjection
{
    /// <summary>A row as the database returns it; its kind becomes the served word once read.</summary>
    public sealed record Row(long Id, string PublicId, string Name, OrganizationKind Kind, string Region, string? WebsiteUrl, int ArticleCount)
    {
        public OrganizationDto Item => new(PublicId, Name, OrganizationKinds.Word(Kind), Region, WebsiteUrl, ArticleCount);
    }

    public static IQueryable<Row> Rows(IQueryable<Organization> organizations, IQueryable<Article> articles)
    {
        var served = ServedRecordFilters.ServedArticles(articles);
        return organizations.Select(organization => new Row(
            organization.Id,
            organization.PublicId,
            organization.Name,
            organization.Kind,
            organization.RegionSlug,
            organization.WebsiteUrl,
            served.Count(article => article.OrganizationId == organization.Id)));
    }
}
