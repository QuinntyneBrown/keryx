using FluentValidation;
using Keryx.Application.Abstractions;
using Keryx.Application.Catalogue;
using Keryx.Domain.Collection;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Runs;

/// <summary>A run in a list: when, how it was started, how it went and its headline counts, all as numbers.</summary>
public sealed record RunSummaryDto(
    string Id,
    string Trigger,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    long? DurationMs,
    string Outcome,
    int Added,
    int SourcesFailed);

/// <summary>A page of runs, with the total that matched.</summary>
public sealed record RunPageDto(IReadOnlyList<RunSummaryDto> Items, int Total, int Page, int PageSize);

/// <summary>
/// A page of one worker's runs, newest first (View run history design, L2-044). <c>problems=true</c>
/// keeps the runs that failed or completed with issues. Values are as the query string gave them.
/// </summary>
public sealed record ListRuns(string? Worker, string? Problems, string? Page, string? PageSize) : IRequest<RunPageDto>
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 50;
}

internal sealed class ListRunsValidator : AbstractValidator<ListRuns>
{
    private static readonly string[] Workers = [.. Enum.GetValues<RunWorker>().Select(RunWords.Worker)];

    public ListRunsValidator()
    {
        RuleFor(request => request.PageSize)
            .Must(size => size is null || ListAdminArticles.TryParseNumber(size, 1, ListRuns.MaxPageSize, out _))
            .WithErrorCode("invalid_page")
            .WithMessage("Give page_size as a whole number from 1 to 50.");
        RuleFor(request => request.Page)
            .Must(page => page is null || ListAdminArticles.TryParseNumber(page, 1, int.MaxValue, out _))
            .WithErrorCode("invalid_page")
            .WithMessage("Give page as a whole number from 1.");
        RuleFor(request => request.Worker)
            .Must(worker => worker is null || Workers.Contains(worker))
            .WithErrorCode("invalid_filter")
            .WithMessage("Give worker as collection, retention or alert_digest.");
        RuleFor(request => request.Problems)
            .Must(problems => problems is null or "true" or "false")
            .WithErrorCode("invalid_filter")
            .WithMessage("Give problems as true or false.");
    }
}

internal sealed class ListRunsHandler(IAppDbContext db) : IRequestHandler<ListRuns, RunPageDto>
{
    public async Task<RunPageDto> Handle(ListRuns request, CancellationToken cancellationToken)
    {
        var worker = request.Worker is null ? RunWorker.Collection : RunWords.WorkerOf(request.Worker);
        var page = request.Page is null ? 1 : int.Parse(request.Page, System.Globalization.CultureInfo.InvariantCulture);
        var size = request.PageSize is null ? ListRuns.DefaultPageSize : int.Parse(request.PageSize, System.Globalization.CultureInfo.InvariantCulture);
        var runs = db.Runs.AsNoTracking().Where(run => run.Worker == worker);
        if (request.Problems == "true")
        {
            runs = runs.Where(run => run.Outcome == RunOutcome.Failed || run.Outcome == RunOutcome.CompletedWithIssues);
        }

        var total = await runs.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await runs.OrderByDescending(run => run.StartedAt).ThenByDescending(run => run.Id)
            .Skip((int)Math.Min((long)(page - 1) * size, int.MaxValue)).Take(size)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return new RunPageDto(
            [.. items.Select(run => new RunSummaryDto(run.PublicId, RunWords.Trigger(run.Trigger), run.StartedAt, run.FinishedAt,
                run.DurationMs, RunWords.Outcome(run.Outcome), run.Counts.Added, run.Counts.SourcesFailed))],
            total, page, size);
    }
}
