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
    int Deleted,
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
            run.SourcesChecked, run.SourcesTotal, counts.Deleted,
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
    bool Muted,
    string State);

internal sealed class GetRunHandler(IAppDbContext db) : IRequestHandler<GetRun, RunDto>
{
    public async Task<RunDto> Handle(GetRun request, CancellationToken cancellationToken)
    {
        var run = await db.Runs.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.PublicId == request.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new RunNotFoundException();
        var occurrences = await db.IssueOccurrences.AsNoTracking()
            .Where(occurrence => occurrence.RunId == run.Id)
            .OrderBy(occurrence => occurrence.Issue.Severity).ThenBy(occurrence => occurrence.Issue.Since)
            .Select(occurrence => new { occurrence.Detail, occurrence.Issue })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var sourceIds = occurrences.Select(occurrence => occurrence.Issue.SourceId).OfType<long>().Distinct().ToList();
        // A removed source still names what it raised or yielded (L2-003 AC3).
        var sources = await db.Sources.AsNoTracking().IgnoreQueryFilters().Where(source => sourceIds.Contains(source.Id))
            .ToDictionaryAsync(source => source.Id, cancellationToken).ConfigureAwait(false);
        return RunDto.From(run, [.. occurrences.Select(occurrence => IssueOf(occurrence.Issue, occurrence.Detail, sources))]);
    }

    /// <summary>An issue as listed on a run: that run's detail, and what it concerns.</summary>
    internal static RunIssueDto IssueOf(RunIssue issue, string detail, Dictionary<long, Keryx.Domain.Sources.Source> sources)
    {
        var (type, id, name) = IssueSubject.Of(issue, sources);
        return new RunIssueDto(issue.PublicId, RunWords.Severity(issue.Severity), RunWords.Kind(issue.Kind), type, id, name, detail,
            issue.ConsecutiveRuns, issue.Since, issue.State == IssueState.Muted, RunWords.State(issue.State));
    }
}

/// <summary>What an issue concerns: a source by its organization's name, a stage, or the worker itself.</summary>
internal static class IssueSubject
{
    public static (string Type, string? Id, string? Name) Of(RunIssue issue, Dictionary<long, Keryx.Domain.Sources.Source> sources) => issue switch
    {
        { SourceId: { } sourceId } when sources.TryGetValue(sourceId, out var source) => ("source", source.PublicId, source.OrganizationName),
        { Stage: { } stage } => ("stage", RunWords.Stage(stage), null),
        _ => ("worker", RunWords.Worker(issue.Worker), null),
    };
}

/// <summary>No run has the id asked for; the code follows the <c>…_not_found</c> pattern of L2-083.</summary>
public sealed class RunNotFoundException() : KeryxException(404, "run_not_found", "The run was not found.");
