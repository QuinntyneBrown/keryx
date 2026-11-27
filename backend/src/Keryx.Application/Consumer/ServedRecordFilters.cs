using Keryx.Domain.Catalogue;
using Keryx.Domain.Collection;

namespace Keryx.Application.Consumer;

/// <summary>What "served" means across /v1, in one place (L2-015 AC3, L2-026 AC5).</summary>
public static class ServedRecordFilters
{
    /// <summary>Published articles with a title: held, hidden and untitled ones are never served.</summary>
    public static IQueryable<Article> ServedArticles(IQueryable<Article> articles) =>
        articles.Where(article => article.Status == PublicationStatus.Published && article.Title != null);
}
