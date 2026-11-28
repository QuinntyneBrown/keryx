using FluentValidation;
using Keryx.Application.Abstractions;
using Keryx.Application.Audit;
using Keryx.Application.Errors;
using Keryx.Domain.Audit;
using Keryx.Domain.Catalogue;
using Keryx.Domain.Collection;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Catalogue;

/// <summary>Staff publish an article whatever its score (L2-014 AC1), in their session.</summary>
public sealed record PublishArticle(string Id, string Session) : IRequest<ArticleDecisionDto>;

/// <summary>Staff hide an article from the consumer API whatever its score (L2-014 AC2), in their session.</summary>
public sealed record HideArticle(string Id, string Session) : IRequest<ArticleDecisionDto>;

/// <summary>Staff undo their last decision on an article, naming the status it replaced (L2-014 AC4).</summary>
public sealed record RevertArticleDecision(string Id, string? PreviousStatus, string Session) : IRequest<ArticleDecisionDto>;

internal sealed class RevertArticleDecisionValidator : AbstractValidator<RevertArticleDecision>
{
    public RevertArticleDecisionValidator()
    {
        RuleFor(request => request.PreviousStatus)
            .Must(status => status is not null && StatusWords.ContainsKey(status))
            .WithErrorCode("invalid_status")
            .WithMessage("previous_status must be published, needs_review or hidden.");
    }

    internal static readonly IReadOnlyDictionary<string, PublicationStatus> StatusWords = new Dictionary<string, PublicationStatus>(StringComparer.Ordinal)
    {
        ["published"] = PublicationStatus.Published,
        ["needs_review"] = PublicationStatus.NeedsReview,
        ["hidden"] = PublicationStatus.Hidden,
    };
}

/// <summary>The decision cannot be undone: another session made it, it replaced another status, or it was undone.</summary>
public sealed class RevertUnavailableException() : KeryxException(409, "revert_unavailable", "This decision can no longer be undone.");

/// <summary>
/// An article's status after a decision: who set it, and the status before staff last decided it,
/// which Undo sends back (null when staff never decided it).
/// </summary>
public sealed record ArticleDecisionDto(string Id, string Status, string DecidedBy, string? PreviousStatus)
{
    internal static ArticleDecisionDto Of(Article article) => new(
        article.PublicId,
        GetAdminArticleHandler.StatusWord(article.Status),
        article.DecidedBy == DecisionMaker.Staff ? "staff" : "system",
        article.PreviousStatus is { } previous ? GetAdminArticleHandler.StatusWord(previous) : null);
}

/// <summary>
/// Publish, hide and Undo: each loads the article, decides, and saves only a change, with its audit
/// entry, so a repeat writes nothing and records nothing (L2-085).
/// </summary>
internal sealed class DecideOnArticleHandler(IAppDbContext db, IAuditTrail audit, TimeProvider time) :
    IRequestHandler<PublishArticle, ArticleDecisionDto>,
    IRequestHandler<HideArticle, ArticleDecisionDto>,
    IRequestHandler<RevertArticleDecision, ArticleDecisionDto>
{
    public Task<ArticleDecisionDto> Handle(PublishArticle request, CancellationToken cancellationToken) =>
        DecideAsync(request.Id, AuditAction.ArticlePublished, (article, now) => article.Publish(now, request.Session), cancellationToken);

    public Task<ArticleDecisionDto> Handle(HideArticle request, CancellationToken cancellationToken) =>
        DecideAsync(request.Id, AuditAction.ArticleHidden, (article, now) => article.Hide(now, request.Session), cancellationToken);

    public Task<ArticleDecisionDto> Handle(RevertArticleDecision request, CancellationToken cancellationToken) =>
        DecideAsync(request.Id, AuditAction.ArticleReverted, (article, _) => article.RevertTo(RevertArticleDecisionValidator.StatusWords[request.PreviousStatus!], request.Session)
            ? true
            : throw new RevertUnavailableException(), cancellationToken);

    private async Task<ArticleDecisionDto> DecideAsync(string id, AuditAction action, Func<Article, DateTimeOffset, bool> decide, CancellationToken cancellationToken)
    {
        var article = await db.Articles.SingleOrDefaultAsync(candidate => candidate.PublicId == id, cancellationToken).ConfigureAwait(false)
            ?? throw new ArticleNotFoundException();
        if (decide(article, time.GetUtcNow()))
        {
            audit.Record(action, article.PublicId);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return ArticleDecisionDto.Of(article);
    }
}
