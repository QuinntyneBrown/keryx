using Keryx.Application.Abstractions;
using Keryx.Domain;
using Keryx.Domain.Collection;
using Keryx.Domain.Sources;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Runs;

/// <summary>The shell's schedule (L2-042 AC1, AC3): the next collection, the interval and the failing sources.</summary>
public sealed record GetShellSchedule : IRequest<ShellScheduleDto>;

/// <summary>
/// The next run's time in UTC, the schedule's interval in hours, the zone the admin app formats
/// times in, and how many sources are failing.
/// </summary>
public sealed record ShellScheduleDto(DateTimeOffset NextRunAt, int IntervalHours, string TimeZone, int FailingSources);

internal sealed class GetShellScheduleHandler(IAppDbContext db, TimeProvider time) : IRequestHandler<GetShellSchedule, ShellScheduleDto>
{
    public async Task<ShellScheduleDto> Handle(GetShellSchedule request, CancellationToken cancellationToken)
    {
        var settings = await db.Settings.AsNoTracking().SingleAsync(cancellationToken).ConfigureAwait(false);
        var next = CollectionSchedule.NextSlotAfter(time.GetUtcNow(), ScheduleInterval.From(settings.IntervalHours), TorontoTime.Zone);
        var failing = await db.Sources.CountAsync(source => source.Health == SourceHealth.Failing, cancellationToken).ConfigureAwait(false);
        return new ShellScheduleDto(next.ToUniversalTime(), settings.IntervalHours, TorontoTime.ZoneId, failing);
    }
}
