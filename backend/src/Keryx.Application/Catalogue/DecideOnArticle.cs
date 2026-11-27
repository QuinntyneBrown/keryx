using Keryx.Application.Abstractions;
using Keryx.Domain.Catalogue;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Catalogue;

/// <summary>Staff publish an article whatever its score (L2-014 AC1).</summary>
public sealed record PublishArticle(string Id) : IRequest<ArticleDecisionDto>;

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

internal sealed class PublishArticleHandler(IAppDbContext db, TimeProvider time) : IRequestHandler<PublishArticle, ArticleDecisionDto>
{
    public async Task<ArticleDecisionDto> Handle(PublishArticle request, CancellationToken cancellationToken)
    {
        var article = await db.Articles.SingleOrDefaultAsync(candidate => candidate.PublicId == request.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new ArticleNotFoundException();
        if (article.Publish(time.GetUtcNow()))
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return ArticleDecisionDto.Of(article);
    }
}
