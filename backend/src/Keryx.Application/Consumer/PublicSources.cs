using System.Globalization;
using FluentValidation;
using Keryx.Application.Abstractions;
using Keryx.Application.Sources;
using Keryx.Domain.Sources;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Consumer;

/// <summary>
/// A source as products read it (L2-029 AC4): where Keryx's information comes from, and nothing
/// of how collecting it goes. Its name is the label staff gave it, never a news query's text.
/// </summary>
public sealed record PublicSourceDto(string Name, string Kind, OrganizationRefDto? Organization, string Region);

/// <summary>A page of active sources by name.</summary>
public sealed record ListPublicSources(string? Limit, string? Cursor) : IRequest<PageDto<PublicSourceDto>>
{
    public const string ListName = "sources";
}

internal sealed class ListPublicSourcesValidator : AbstractValidator<ListPublicSources>
{
    public ListPublicSourcesValidator(ICursorCodec cursors)
    {
        RuleFor(request => request.Limit)
            .Must(limit => limit is null || PageLimit.TryParse(limit, out _))
            .WithErrorCode("invalid_limit")
            .WithMessage("Give limit as a whole number from 1 to 100.");
        RuleFor(request => request.Cursor)
            .Must(cursor => cursor is null || cursors.Decode(cursor) is { List: ListPublicSources.ListName })
            .WithErrorCode("invalid_cursor")
            .WithMessage("Use the next_cursor of the previous page.");
    }
}

/// <summary>
/// One keyset query over sources ordered by (name, id). It selects four columns and the source's
/// listed organization, so health, failure, address and query text cannot reach the response.
/// Every stored source is active until staff can remove one (journey 17), which filters here.
/// </summary>
internal sealed class ListPublicSourcesHandler(IAppDbContext db, ICursorCodec cursors) : IRequestHandler<ListPublicSources, PageDto<PublicSourceDto>>
{
    public async Task<PageDto<PublicSourceDto>> Handle(ListPublicSources request, CancellationToken cancellationToken)
    {
        var limit = request.Limit is null ? PageLimit.Default : int.Parse(request.Limit, CultureInfo.InvariantCulture);
        IQueryable<Source> query = db.Sources.AsNoTracking();
        if (request.Cursor is not null && cursors.Decode(request.Cursor) is { } after)
        {
            // The database's collation orders the names and compares them, so the seek agrees with the sort.
            query = query.Where(source => source.OrganizationName.CompareTo(after.Sort) > 0
                || (source.OrganizationName == after.Sort && source.Id > after.Id));
        }

        // An organization is named only once it is listed, so its id always reads on /v1/organizations.
        var listed = ServedRecordFilters.ListedOrganizations(db.Organizations, db.Articles, db.Events);
        var rows = await query
            .OrderBy(source => source.OrganizationName).ThenBy(source => source.Id)
            .Take(limit + 1)
            .Select(source => new
            {
                source.Id,
                source.OrganizationName,
                source.Kind,
                source.RegionSlug,
                Organization = listed
                    .Where(organization => organization.Provenance.Any(entry => entry.SourceId == source.Id))
                    .Select(organization => new OrganizationRefDto(organization.PublicId, organization.Name))
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var items = rows.Take(limit).Select(row => new PublicSourceDto(row.OrganizationName, SourceWords.Kind(row.Kind), row.Organization, row.RegionSlug)).ToList();
        if (rows.Count <= limit)
        {
            return new PageDto<PublicSourceDto>(items, null);
        }

        var last = rows[limit - 1];
        return new PageDto<PublicSourceDto>(items, cursors.Encode(new CursorPosition(ListPublicSources.ListName, string.Empty, last.OrganizationName, last.Id)));
    }
}
