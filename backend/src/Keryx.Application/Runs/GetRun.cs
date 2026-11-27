using Keryx.Application.Abstractions;
using Keryx.Application.Errors;
using Keryx.Domain.Collection;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Runs;

/// <summary>One run by its public id (L2-019).</summary>
public sealed record GetRun(string Id) : IRequest<RunDto>;

/// <summary>A run as staff read it: every count a number, its six stages in order, and its issues.</summary>
public sealed record RunDto(
    string Id,
    string Worker,
    string Trigger,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    long? DurationMs,
    string Outcome,
    string? FailureReason,
    string? CorrelationId,
    int Added,
    int Updated,
    int Unchanged,
    int Duplicates,
    int Held,
    int OffTopic,
    int Rejected,
    int SourcesOk,
    int SourcesFailed,
    int SourcesChecked,
    int SourcesTotal,
    IReadOnlyList<StageResultDto> Stages,
    IReadOnlyList<RunIssueDto> Issues)
{
    public static RunDto From(Run run, IReadOnlyList<RunIssueDto> issues)
    {
        ArgumentNullException.ThrowIfNull(run);
        var counts = run.Counts;
        return new RunDto(
            run.PublicId, RunWords.Worker(run.Worker), RunWords.Trigger(run.Trigger), run.StartedAt, run.FinishedAt, run.DurationMs,
            RunWords.Outcome(run.Outcome), run.FailureReason, run.CorrelationId, counts.Added, counts.Updated, counts.Unchanged,
            counts.Duplicates, counts.Held, counts.OffTopic, counts.Rejected, counts.SourcesOk, counts.SourcesFailed,
            run.SourcesChecked, run.SourcesTotal,
            [.. run.Stages.OrderBy(stage => stage.Stage).Select(stage => new StageResultDto(
                RunWords.Stage(stage.Stage), RunWords.Status(stage.Status), stage.DurationMs,
                new Dictionary<string, int>(stage.Counts), stage.Reason))],
            issues);
    }
}

/// <summary>One stage of a run: its status, duration and named counts (L2-007).</summary>
public sealed record StageResultDto(string Stage, string Status, long DurationMs, IReadOnlyDictionary<string, int> Counts, string? Reason);

/// <summary>A problem a run found (L2-020).</summary>
public sealed record RunIssueDto(
    string Id,
    string Severity,
    string Kind,
    string SubjectType,
    string? SubjectId,
    string? SubjectName,
    string Detail,
    int ConsecutiveRuns,
    DateTimeOffset Since,
    bool Muted);

internal sealed class GetRunHandler(IAppDbContext db) : IRequestHandler<GetRun, RunDto>
{
    public async Task<RunDto> Handle(GetRun request, CancellationToken cancellationToken)
    {
        var run = await db.Runs.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.PublicId == request.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new RunNotFoundException();
        return RunDto.From(run, []);
    }
}

/// <summary>No run has the id asked for; the code follows the <c>…_not_found</c> pattern of L2-083.</summary>
public sealed class RunNotFoundException() : KeryxException(404, "run_not_found", "The run was not found.");
