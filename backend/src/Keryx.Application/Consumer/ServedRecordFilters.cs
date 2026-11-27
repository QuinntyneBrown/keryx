using Keryx.Domain.Catalogue;
using Keryx.Domain.Collection;

namespace Keryx.Application.Consumer;

/// <summary>What "served" means across /v1, in one place (L2-015 AC3, L2-026 AC5).</summary>
public static class ServedRecordFilters
{
    /// <summary>Published articles with a title: held, hidden and untitled ones are never served.</summary>
    public static IQueryable<Article> ServedArticles(IQueryable<Article> articles) =>
        articles.Where(article => article.Status == PublicationStatus.Published && article.Title != null);

    /// <summary>
    /// Events that follow their article: served while it is served, so hiding the article removes
    /// the event; an event with no article (its article was removed) is served on its own status.
    /// </summary>
    public static IQueryable<EventRecord> ServedEvents(IQueryable<EventRecord> events) =>
        events.Where(item => item.Title != null && (item.LinkedArticle != null
            ? item.LinkedArticle.Status == PublicationStatus.Published && item.LinkedArticle.Title != null
            : item.Status == PublicationStatus.Published));
}
