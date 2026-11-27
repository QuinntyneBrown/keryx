using FluentValidation;
using Keryx.Application.Abstractions;
using Keryx.Application.Catalogue;
using Keryx.Domain.Collection;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Runs;

/// <summary>
/// An open issue as Overview lists it (L2-045): its severity and kind, what it concerns, its latest
/// detail, its streak, and the run it last occurred in, which a card opens.
/// </summary>
public sealed record OpenIssueDto(
    string Id,
    string RunId,
    string Severity,
    string Kind,
    string SubjectType,
    string? SubjectId,
    string? SubjectName,
    string Detail,
    int ConsecutiveRuns,
    DateTimeOffset Since);

/// <summary>The most severe open issues, with how many are open in all.</summary>
public sealed record OpenIssuePageDto(IReadOnlyList<OpenIssueDto> Items, int Total, int PageSize);

/// <summary>
/// The open issues that need attention, most severe first: errors before warnings, then the longest
/// streak, then the oldest (L2-045 AC2, AC6). Muted and closed issues are left out.
/// </summary>
public sealed record ListOpenIssues(string? State, string? PageSize) : IRequest<OpenIssuePageDto>
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 50;
}

internal sealed class ListOpenIssuesValidator : AbstractValidator<ListOpenIssues>
{
    public ListOpenIssuesValidator()
    {
        RuleFor(request => request.State)
            .Must(state => state is null or "open")
            .WithErrorCode("invalid_filter")
            .WithMessage("Give state as open.");
        RuleFor(request => request.PageSize)
            .Must(size => size is null || ListAdminArticles.TryParseNumber(size, 1, ListOpenIssues.MaxPageSize, out _))
            .WithErrorCode("invalid_page")
            .WithMessage("Give page_size as a whole number from 1 to 50.");
    }
}

internal sealed class ListOpenIssuesHandler(IAppDbContext db) : IRequestHandler<ListOpenIssues, OpenIssuePageDto>
{
    public async Task<OpenIssuePageDto> Handle(ListOpenIssues request, CancellationToken cancellationToken)
    {
        var size = request.PageSize is null ? ListOpenIssues.DefaultPageSize : int.Parse(request.PageSize, System.Globalization.CultureInfo.InvariantCulture);
        var open = db.RunIssues.AsNoTracking().Where(issue => issue.State == IssueState.Open);
        var total = await open.CountAsync(cancellationToken).ConfigureAwait(false);
        var issues = await open
            .OrderBy(issue => issue.Severity).ThenByDescending(issue => issue.ConsecutiveRuns).ThenBy(issue => issue.Since).ThenBy(issue => issue.Id)
            .Take(size)
            .Select(issue => new
            {
                Issue = issue,
                RunId = db.IssueOccurrences.Where(occurrence => occurrence.IssueId == issue.Id)
                    .OrderByDescending(occurrence => occurrence.OccurredAt).ThenByDescending(occurrence => occurrence.Id)
                    .Select(occurrence => occurrence.Run.PublicId).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var sourceIds = issues.Select(found => found.Issue.SourceId).OfType<long>().Distinct().ToList();
        var sources = await db.Sources.AsNoTracking().Where(source => sourceIds.Contains(source.Id))
            .ToDictionaryAsync(source => source.Id, cancellationToken).ConfigureAwait(false);
        return new OpenIssuePageDto(
            [.. issues.Select(found =>
            {
                var (type, id, name) = IssueSubject.Of(found.Issue, sources);
                return new OpenIssueDto(found.Issue.PublicId, found.RunId ?? string.Empty, RunWords.Severity(found.Issue.Severity),
                    RunWords.Kind(found.Issue.Kind), type, id, name, found.Issue.Detail, found.Issue.ConsecutiveRuns, found.Issue.Since);
            })],
            total, size);
    }
}
