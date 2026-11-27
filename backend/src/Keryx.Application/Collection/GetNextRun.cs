using Keryx.Application.Abstractions;
using Keryx.Domain;
using Keryx.Domain.Collection;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Collection;

/// <summary>When the next scheduled collection run is due (L2-005 AC2).</summary>
public sealed record GetNextRun : IRequest<NextRunDto>;

/// <summary>The next run's time in UTC, with the zone the admin app formats it in.</summary>
public sealed record NextRunDto(DateTimeOffset NextRunAt, string TimeZone);

internal sealed class GetNextRunHandler(IAppDbContext db, TimeProvider time) : IRequestHandler<GetNextRun, NextRunDto>
{
    public async Task<NextRunDto> Handle(GetNextRun request, CancellationToken cancellationToken)
    {
        var settings = await db.Settings.AsNoTracking().SingleAsync(cancellationToken).ConfigureAwait(false);
        var next = CollectionSchedule.NextSlotAfter(time.GetUtcNow(), ScheduleInterval.From(settings.IntervalHours), TorontoTime.Zone);
        return new NextRunDto(next.ToUniversalTime(), TorontoTime.ZoneId);
    }
}
