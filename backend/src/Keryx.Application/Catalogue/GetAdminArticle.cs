using Keryx.Application.Abstractions;
using Keryx.Application.Errors;
using Keryx.Domain.Collection;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Catalogue;

/// <summary>One article by its public id, as staff read it (L2-011, L2-015).</summary>
public sealed record GetAdminArticle(string Id) : IRequest<AdminArticleDto>;

/// <summary>An article with its provenance. Its text came from a source: a client renders it as text.</summary>
public sealed record AdminArticleDto(
    string Id,
    string? Title,
    string? Organization,
    string Category,
    string Region,
    decimal Relevance,
    string Status,
    DateTimeOffset? PublishedAt,
    DateTimeOffset CollectedAt,
    string Summary,
    string Url,
    ArticleSourceDto? Source,
    IReadOnlyList<string> Tags,
    IReadOnlyList<ProvenanceDto> Provenance);

/// <summary>The source an article was first collected from.</summary>
public sealed record ArticleSourceDto(string Id, string Name);

/// <summary>Where a fact came from: the source (its id null once removed), its name and the URL read, first and last seen.</summary>
public sealed record ProvenanceDto(string? SourceId, string SourceName, string Url, DateTimeOffset FirstSeenAt, DateTimeOffset LastSeenAt);

internal sealed class GetAdminArticleHandler(IAppDbContext db) : IRequestHandler<GetAdminArticle, AdminArticleDto>
{
    public async Task<AdminArticleDto> Handle(GetAdminArticle request, CancellationToken cancellationToken)
    {
        var article = await db.Articles.AsNoTracking().Include(candidate => candidate.Organization)
            .SingleOrDefaultAsync(candidate => candidate.PublicId == request.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new ArticleNotFoundException();
        var sourceIds = article.Provenance.Select(entry => entry.SourceId).Append(article.SourceId).OfType<long>().Distinct().ToList();
        var sources = await db.Sources.AsNoTracking().Where(source => sourceIds.Contains(source.Id))
            .ToDictionaryAsync(source => source.Id, cancellationToken).ConfigureAwait(false);
        var first = article.SourceId is { } id && sources.TryGetValue(id, out var source) ? new ArticleSourceDto(source.PublicId, source.OrganizationName) : null;
        return new AdminArticleDto(
            article.PublicId, article.Title, article.Organization?.Name, article.Category, article.RegionSlug, article.Score, StatusWord(article.Status),
            article.PublishedAt, article.CollectedAt, article.Summary, article.Url, first, article.Tags,
            [.. article.Provenance.OrderBy(entry => entry.FirstSeenAt).Select(entry => new ProvenanceDto(
                entry.SourceId is { } sourceId && sources.TryGetValue(sourceId, out var named) ? named.PublicId : null,
                entry.SourceName, entry.Url, entry.FirstSeenAt, entry.LastSeenAt))]);
    }

    public static string StatusWord(PublicationStatus status) => status switch
    {
        PublicationStatus.Published => "published",
        PublicationStatus.NeedsReview => "needs_review",
        _ => "hidden",
    };
}

/// <summary>No article has the id asked for.</summary>
public sealed class ArticleNotFoundException() : KeryxException(404, "article_not_found", "The article was not found.");
