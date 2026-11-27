using System.Globalization;
using Keryx.Application.Abstractions;
using Keryx.Application.Sources;
using Keryx.Domain.Collection;
using Keryx.Domain.Sources;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Collection;

/// <summary>
/// Keeps each source's issues across collection runs (L2-020). A problem that occurred in the
/// worker's previous run continues its issue, growing the streak from the same <c>since</c>; any
/// other is a new issue with a streak of 1. A source that answers without a problem closes that
/// problem's issue, which also clears a mute, so a later recurrence is a new open issue. Every
/// detail is sanitized before it is stored.
/// </summary>
public sealed class SourceIssueTracker(IAppDbContext db)
{
    private static readonly IssueKind[] FetchKinds =
        [IssueKind.SourceNotFound, IssueKind.SourceTimeout, IssueKind.SourceBlocked, IssueKind.SourceInvalid];

    /// <summary>The source's fetch failed after its final attempt.</summary>
    public async Task FailedAsync(Run run, long? previousRunId, Source source, FetchFailureReason reason, TimeSpan timeout, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        var kind = KindOf(reason);
        await CloseAsync(source, [.. FetchKinds.Where(other => other != kind), IssueKind.ItemsHeld], cancellationToken).ConfigureAwait(false);
        await TrackAsync(run, previousRunId, source, IssueSeverity.Error, kind, FetchDetail(reason, source.Address, timeout), now, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The source answered, with <paramref name="held"/> items held for review.</summary>
    public async Task SucceededAsync(Run run, long? previousRunId, Source source, int held, decimal threshold, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        await CloseAsync(source, held > 0 ? FetchKinds : [.. FetchKinds, IssueKind.ItemsHeld], cancellationToken).ConfigureAwait(false);
        if (held > 0)
        {
            var detail = string.Create(CultureInfo.InvariantCulture,
                $"{held} {(held == 1 ? "item" : "items")} held for review: {(held == 1 ? "its" : "their")} relevance score is between {PublicationPolicy.ReviewFloor:0.00} and {threshold:0.00}.");
            await TrackAsync(run, previousRunId, source, IssueSeverity.Warning, IssueKind.ItemsHeld, detail, now, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task TrackAsync(
        Run run, long? previousRunId, Source source, IssueSeverity severity, IssueKind kind, string detail, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var safe = IssueDetailSanitizer.Sanitize(detail);
        var current = await db.RunIssues
            .SingleOrDefaultAsync(issue => issue.SourceId == source.Id && issue.Kind == kind && issue.State != IssueState.Closed, cancellationToken)
            .ConfigureAwait(false);
        var continues = current is not null && previousRunId is { } previous && await db.IssueOccurrences
            .AnyAsync(occurrence => occurrence.IssueId == current.Id && occurrence.RunId == previous, cancellationToken).ConfigureAwait(false);
        if (continues)
        {
            current!.Continue(safe);
        }
        else
        {
            // A gap of a run or more ends a streak: the old issue closes and a new one opens.
            current?.Close();
            current = RunIssue.ForSource(run.Worker, severity, kind, source.Id, safe, now);
            db.RunIssues.Add(current);
        }

        db.IssueOccurrences.Add(IssueOccurrence.Of(current, run, now));
    }

    private async Task CloseAsync(Source source, IssueKind[] kinds, CancellationToken cancellationToken)
    {
        var open = await db.RunIssues
            .Where(issue => issue.SourceId == source.Id && kinds.Contains(issue.Kind) && issue.State != IssueState.Closed)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var issue in open)
        {
            issue.Close();
        }
    }

    private static IssueKind KindOf(FetchFailureReason reason) => reason switch
    {
        FetchFailureReason.NotFound => IssueKind.SourceNotFound,
        FetchFailureReason.Timeout => IssueKind.SourceTimeout,
        FetchFailureReason.InvalidContent => IssueKind.SourceInvalid,
        _ => IssueKind.SourceBlocked,
    };

    private static string FetchDetail(FetchFailureReason reason, string address, TimeSpan timeout) => reason switch
    {
        FetchFailureReason.NotFound => $"Nothing was found at {address}.",
        FetchFailureReason.Timeout => string.Create(CultureInfo.InvariantCulture, $"The source did not answer within {timeout.TotalSeconds:0}s on 3 attempts."),
        FetchFailureReason.BlockedByRobots => $"The site's robots.txt asks Keryx not to read {address}.",
        FetchFailureReason.InvalidContent => $"{address} answered with nothing Keryx can read.",
        FetchFailureReason.UnsafeAddress => "The address does not lead to the public internet.",
        _ => $"The site refused the request for {address}.",
    };
}
