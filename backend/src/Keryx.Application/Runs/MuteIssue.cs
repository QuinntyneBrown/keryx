using Keryx.Application.Abstractions;
using Keryx.Application.Audit;
using Keryx.Application.Errors;
using Keryx.Domain.Audit;
using Keryx.Domain.Collection;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Runs;

/// <summary>Staff mute an open issue: it is not counted as open and raises no alerts (L2-020 AC2).</summary>
public sealed record MuteIssueCommand(string Id) : IRequest<RunIssueDto>;

/// <summary>Staff unmute a muted issue: it is open again (L2-020 AC3).</summary>
public sealed record UnmuteIssueCommand(string Id) : IRequest<RunIssueDto>;

/// <summary>
/// Mutes or unmutes one issue and answers it as it now is. Asking for what already holds changes
/// nothing; an issue that is over is refused, since its source has recovered.
/// </summary>
internal sealed class MuteIssueHandler(IAppDbContext db, IAuditTrail audit) : IRequestHandler<MuteIssueCommand, RunIssueDto>, IRequestHandler<UnmuteIssueCommand, RunIssueDto>
{
    public Task<RunIssueDto> Handle(MuteIssueCommand request, CancellationToken cancellationToken) =>
        ChangeAsync(request.Id, AuditAction.IssueMuted, issue => issue.Mute(), cancellationToken);

    public Task<RunIssueDto> Handle(UnmuteIssueCommand request, CancellationToken cancellationToken) =>
        ChangeAsync(request.Id, AuditAction.IssueUnmuted, issue => issue.Unmute(), cancellationToken);

    private async Task<RunIssueDto> ChangeAsync(string id, AuditAction action, Action<RunIssue> change, CancellationToken cancellationToken)
    {
        var issue = await db.RunIssues.SingleOrDefaultAsync(candidate => candidate.PublicId == id, cancellationToken).ConfigureAwait(false)
            ?? throw new IssueNotFoundException();
        if (issue.State == IssueState.Closed)
        {
            throw new IssueClosedException();
        }

        // A repeat changes nothing and records nothing (L2-085).
        var before = issue.State;
        change(issue);
        if (issue.State != before)
        {
            audit.Record(action, issue.PublicId);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var sources = await db.Sources.AsNoTracking().IgnoreQueryFilters().Where(source => source.Id == issue.SourceId)
            .ToDictionaryAsync(source => source.Id, cancellationToken).ConfigureAwait(false);
        return GetRunHandler.IssueOf(issue, issue.Detail, sources);
    }
}

/// <summary>No issue has the id asked for (L2-083).</summary>
public sealed class IssueNotFoundException() : KeryxException(404, "issue_not_found", "The issue was not found.");

/// <summary>The issue is over: its source recovered, so there is nothing to mute or unmute.</summary>
public sealed class IssueClosedException() : KeryxException(409, "issue_closed", "The issue is over.");
