using Keryx.Application.Abstractions;
using Keryx.Application.Catalogue;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Consumer;

/// <summary>One served article by its public id (L2-027).</summary>
public sealed record GetArticle(string Id) : IRequest<ArticleDetailDto>;

/// <summary>An article that is not served, and any unknown or malformed id, are the same 404 (L2-027 AC2).</summary>
internal sealed class GetArticleHandler(IAppDbContext db) : IRequestHandler<GetArticle, ArticleDetailDto>
{
    public async Task<ArticleDetailDto> Handle(GetArticle request, CancellationToken cancellationToken)
    {
        var article = await ServedRecordFilters.ServedArticles(db.Articles).AsNoTracking().Include(candidate => candidate.Organization)
            .SingleOrDefaultAsync(candidate => candidate.PublicId == request.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new ArticleNotFoundException();
        var eventId = await db.Events.AsNoTracking().Where(item => item.LinkedArticleId == article.Id)
            .OrderBy(item => item.Id).Select(item => item.PublicId).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return new ArticleDetailDto(
            article.PublicId,
            article.Title!,
            article.Organization is { } organization ? new OrganizationRefDto(organization.PublicId, organization.Name) : null,
            article.Category,
            article.RegionSlug,
            article.PublishedAt,
            article.Url,
            article.Summary,
            article.Tags,
            article.CollectedAt,
            eventId,
            [.. article.Provenance.OrderBy(entry => entry.FirstSeenAt).Select(entry => new SourceLinkDto(entry.SourceName, entry.Url))]);
    }
}
