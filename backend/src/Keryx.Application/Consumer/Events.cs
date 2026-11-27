using System.Globalization;
using FluentValidation;
using Keryx.Application.Abstractions;
using Keryx.Application.Errors;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Consumer;

/// <summary>An event as products read it (L2-028 AC4): its start in UTC with the zone to show it in.</summary>
public sealed record EventDto(
    string Id, string Title, DateTimeOffset StartsAt, DateTimeOffset? EndsAt, string TimeZone, string PlaceName, string Region,
    OrganizationRefDto? Organization, string Url, string? ArticleId);

/// <summary>
/// A page of upcoming served events, soonest first, starting in <c>from</c>..<c>to</c> and matching
/// every filter given (L2-028). Values are as the query string gave them.
/// </summary>
public sealed record ListEvents(string? Limit, string? Cursor, string? From, string? To, string? Region, string? Org)
    : IRequest<PageDto<EventDto>>
{
    public const string ListName = "events";

    /// <summary>The longest range a product may ask for.</summary>
    public static readonly TimeSpan MaxRange = TimeSpan.FromDays(366);

    /// <summary>The filters a cursor is bound to.</summary>
    public string Filters =>
        string.Join('&', $"from={Ticks(From)}", $"to={Ticks(To)}", $"region={Region}", $"org={Org}");

    private static string Ticks(string? time) =>
        FilterTime.TryParse(time, out var at) ? at.UtcTicks.ToString(CultureInfo.InvariantCulture) : string.Empty;
}

/// <summary>
/// The limit, the cursor and each filter, refused with the code a product branches on. A range is
/// measured from <c>from</c>, or from now when only <c>to</c> is given, and may be at most 366 days.
/// </summary>
internal sealed class ListEventsValidator : AbstractValidator<ListEvents>
{
    public ListEventsValidator(IAppDbContext db, ICursorCodec cursors, TimeProvider time)
    {
        RuleFor(request => request.Limit)
            .Must(limit => limit is null || PageLimit.TryParse(limit, out _))
            .WithErrorCode("invalid_limit")
            .WithMessage("Give limit as a whole number from 1 to 100.");
        RuleFor(request => request.From)
            .Must(from => from is null || FilterTime.TryParse(from, out _))
            .WithErrorCode("invalid_filter")
            .WithMessage("Give from as an ISO 8601 time with its offset, such as 2026-12-01T00:00:00Z.");
        RuleFor(request => request.To)
            .Must(to => to is null || FilterTime.TryParse(to, out _))
            .WithErrorCode("invalid_filter")
            .WithMessage("Give to as an ISO 8601 time with its offset, such as 2026-12-31T23:59:59Z.");
        RuleFor(request => request)
            .Must(request => !FilterTime.TryParse(request.From, out var from) || !FilterTime.TryParse(request.To, out var to) || to >= from)
            .WithName("to")
            .WithErrorCode("invalid_filter")
            .WithMessage("Give a to that is not earlier than from.");
        RuleFor(request => request)
            .Must(request => !FilterTime.TryParse(request.To, out var to)
                || to - (FilterTime.TryParse(request.From, out var from) ? from : time.GetUtcNow()) <= ListEvents.MaxRange)
            .WithName("to")
            .WithErrorCode("invalid_filter")
            .WithMessage("Ask for a range of at most 366 days.");
        RuleFor(request => request.Region)
            .MustAsync(async (region, cancellationToken) => region is null
                || await db.Regions.AnyAsync(known => known.Slug == region, cancellationToken).ConfigureAwait(false))
            .WithErrorCode("invalid_filter")
            .WithMessage("Give region as a slug that /v1/regions lists.");
        RuleFor(request => request.Org)
            .Must(org => org is null || Keryx.Domain.PublicIds.IsWellFormed(org, "org"))
            .WithErrorCode("invalid_filter")
            .WithMessage("Give org as an organization's id, org_ followed by its characters.");
        RuleFor(request => request.Cursor)
            .Must((request, cursor) => cursor is null
                || cursors.Decode(cursor) is { List: ListEvents.ListName } position && position.Filters == request.Filters)
            .WithErrorCode("invalid_cursor")
            .WithMessage("Use the next_cursor of a page read with the same filters.");
    }
}

/// <summary>
/// One keyset query: served events starting at or after the later of now and <c>from</c>, and at
/// or before <c>to</c>, ordered by (StartsAtUtc, Id) ascending, taking one row more than the limit.
/// </summary>
internal sealed class ListEventsHandler(IAppDbContext db, ICursorCodec cursors, TimeProvider time) : IRequestHandler<ListEvents, PageDto<EventDto>>
{
    public async Task<PageDto<EventDto>> Handle(ListEvents request, CancellationToken cancellationToken)
    {
        var limit = request.Limit is null ? PageLimit.Default : int.Parse(request.Limit, CultureInfo.InvariantCulture);
        var now = time.GetUtcNow();
        var start = FilterTime.TryParse(request.From, out var from) && from > now ? from : now;
        var query = ServedRecordFilters.ServedEvents(db.Events).AsNoTracking().Where(item => item.StartsAtUtc >= start);
        if (FilterTime.TryParse(request.To, out var to))
        {
            query = query.Where(item => item.StartsAtUtc <= to);
        }

        if (request.Region is { } region)
        {
            query = query.Where(item => item.RegionSlug == region);
        }

        if (request.Org is { } org)
        {
            query = query.Where(item => item.Organization != null && item.Organization.PublicId == org);
        }

        if (request.Cursor is not null && cursors.Decode(request.Cursor) is { } after)
        {
            query = query.Where(item => item.StartsAtUtc > after.SortedAt || (item.StartsAtUtc == after.SortedAt && item.Id > after.Id));
        }

        var rows = await EventProjection.Rows(query.OrderBy(item => item.StartsAtUtc).ThenBy(item => item.Id).Take(limit + 1))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (rows.Count <= limit)
        {
            return new PageDto<EventDto>([.. rows.Select(row => row.Item)], null);
        }

        var last = rows[limit - 1];
        return new PageDto<EventDto>(
            [.. rows.Take(limit).Select(row => row.Item)],
            cursors.Encode(new CursorPosition(ListEvents.ListName, request.Filters, last.Item.StartsAt, last.Id)));
    }
}

/// <summary>One served event by its public id (L2-028 AC5); past events stay readable.</summary>
public sealed record GetEvent(string Id) : IRequest<EventDto>;

internal sealed class GetEventHandler(IAppDbContext db) : IRequestHandler<GetEvent, EventDto>
{
    public async Task<EventDto> Handle(GetEvent request, CancellationToken cancellationToken)
    {
        var row = await EventProjection.Rows(ServedRecordFilters.ServedEvents(db.Events).AsNoTracking().Where(item => item.PublicId == request.Id))
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return row?.Item ?? throw new EventNotFoundException();
    }
}

public sealed class EventNotFoundException() : KeryxException(404, "event_not_found", "The event was not found.");

/// <summary>The one projection both reads share, so a listed event and a read one are the same JSON.</summary>
internal static class EventProjection
{
    public sealed record Row(long Id, EventDto Item);

    // The event's link is where it was first seen; its article, when it has one, is that same page.
    public static IQueryable<Row> Rows(IQueryable<Keryx.Domain.Catalogue.EventRecord> events) =>
        events.Select(item => new Row(
            item.Id,
            new EventDto(
                item.PublicId,
                item.Title!,
                item.StartsAtUtc,
                item.EndsAtUtc,
                item.TimeZone,
                item.PlaceName,
                item.RegionSlug,
                item.Organization == null ? null : new OrganizationRefDto(item.Organization.PublicId, item.Organization.Name),
                item.Provenance.OrderBy(entry => entry.FirstSeenAt).Select(entry => entry.Url).First(),
                item.LinkedArticle == null ? null : item.LinkedArticle.PublicId)));
}
