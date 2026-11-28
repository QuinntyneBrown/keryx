using Keryx.Application.Abstractions;
using Keryx.Domain.Collection;

namespace Keryx.Application.Collection;

/// <summary>
/// A run's failed sources, as a retry fetches them again: those whose fetch issue, still open or
/// muted, occurred in that run. A source that has recovered since is not among them, nor one whose
/// failure began in a later run.
/// </summary>
internal static class RetryableSources
{
    public static IQueryable<long> Of(IAppDbContext db, Run run) =>
        db.IssueOccurrences
            .Where(occurrence => occurrence.RunId == run.Id
                && occurrence.Issue.SourceId != null
                && occurrence.Issue.State != IssueState.Closed
                && SourceIssueTracker.FetchKinds.Contains(occurrence.Issue.Kind))
            .Select(occurrence => occurrence.Issue.SourceId!.Value);
}
