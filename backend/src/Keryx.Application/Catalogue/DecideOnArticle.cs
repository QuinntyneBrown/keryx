using Keryx.Application.Abstractions;
using Keryx.Domain.Catalogue;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Catalogue;

/// <summary>Staff publish an article whatever its score (L2-014 AC1).</summary>
public sealed record PublishArticle(string Id) : IRequest<ArticleDecisionDto>;

/// <summary>Staff hide an article from the consumer API whatever its score (L2-014 AC2).</summary>
public sealed record HideArticle(string Id) : IRequest<ArticleDecisionDto>;

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

/// <summary>Publish and hide: each loads the article, decides, and saves only a change, so a repeat writes nothing.</summary>
internal sealed class DecideOnArticleHandler(IAppDbContext db, TimeProvider time) :
    IRequestHandler<PublishArticle, ArticleDecisionDto>,
    IRequestHandler<HideArticle, ArticleDecisionDto>
{
    public Task<ArticleDecisionDto> Handle(PublishArticle request, CancellationToken cancellationToken) =>
        DecideAsync(request.Id, (article, now) => article.Publish(now), cancellationToken);

    public Task<ArticleDecisionDto> Handle(HideArticle request, CancellationToken cancellationToken) =>
        DecideAsync(request.Id, (article, now) => article.Hide(now), cancellationToken);

    private async Task<ArticleDecisionDto> DecideAsync(string id, Func<Article, DateTimeOffset, bool> decide, CancellationToken cancellationToken)
    {
        var article = await db.Articles.SingleOrDefaultAsync(candidate => candidate.PublicId == id, cancellationToken).ConfigureAwait(false)
            ?? throw new ArticleNotFoundException();
        if (decide(article, time.GetUtcNow()))
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return ArticleDecisionDto.Of(article);
    }
}
