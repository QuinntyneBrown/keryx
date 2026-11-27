using Keryx.Application.Abstractions;
using Keryx.Application.Collection;
using Keryx.Domain.Collection;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Runs;

/// <summary>The top of Overview (L2-043, L2-046): the last and current collection runs, what exists, and the next run.</summary>
public sealed record GetOverviewStatus : IRequest<OverviewStatusDto>;

/// <summary>
/// Overview's status: the last finished collection run and the one in progress (each null when
/// there is none), whether any source and any run exist, and the next run's time in UTC with the
/// zone the admin app formats it in.
/// </summary>
public sealed record OverviewStatusDto(
    OverviewRunDto? LastRun,
    OverviewRunDto? CurrentRun,
    bool HasSources,
    bool HasRuns,
    DateTimeOffset NextRunAt,
    string TimeZone);

/// <summary>
/// One collection run as Overview shows it. <c>failure_code</c> is null unless it failed, then
/// <c>&lt;stage&gt;_failed</c> for a stage that failed, <c>interrupted</c> for a run a restart cut
/// short, or <c>unexpected</c>.
/// </summary>
public sealed record OverviewRunDto(
    string Id,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    long? DurationMs,
    string Outcome,
    string? FailureCode,
    int Added,
    int SourcesOk,
    int SourcesChecked,
    int SourcesTotal)
{
    internal static OverviewRunDto From(Run run) => new(
        run.PublicId, run.StartedAt, run.FinishedAt, run.DurationMs, RunWords.Outcome(run.Outcome), FailureCodeOf(run),
        run.Counts.Added, run.Counts.SourcesOk, run.SourcesChecked, run.SourcesTotal);

    private static string? FailureCodeOf(Run run)
    {
        if (run.Outcome != RunOutcome.Failed)
        {
            return null;
        }

        var failed = run.Stages.FirstOrDefault(stage => stage.Status == StageStatus.Failed);
        if (failed is not null)
        {
            return $"{RunWords.Stage(failed.Stage)}_failed";
        }

        return run.FailureReason == RecoverInterruptedRunsHandler.Interrupted ? "interrupted" : "unexpected";
    }
}

internal sealed class GetOverviewStatusHandler(IAppDbContext db, ISender sender) : IRequestHandler<GetOverviewStatus, OverviewStatusDto>
{
    public async Task<OverviewStatusDto> Handle(GetOverviewStatus request, CancellationToken cancellationToken)
    {
        var runs = db.Runs.AsNoTracking().Where(run => run.Worker == RunWorker.Collection);
        var last = await runs.Where(run => run.Outcome != RunOutcome.Running)
            .OrderByDescending(run => run.StartedAt).ThenByDescending(run => run.Id)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var current = await runs.Where(run => run.Outcome == RunOutcome.Running)
            .OrderByDescending(run => run.StartedAt).ThenByDescending(run => run.Id)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var hasSources = await db.Sources.AnyAsync(cancellationToken).ConfigureAwait(false);
        var hasRuns = last is not null || current is not null;
        var next = await sender.Send(new GetNextRun(), cancellationToken).ConfigureAwait(false);
        return new OverviewStatusDto(
            last is null ? null : OverviewRunDto.From(last),
            current is null ? null : OverviewRunDto.From(current),
            hasSources, hasRuns, next.NextRunAt, next.TimeZone);
    }
}
