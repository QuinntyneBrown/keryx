using Keryx.Application.Abstractions;
using Keryx.Application.Sources;
using Keryx.Domain.Catalogue;
using Keryx.Domain.Collection;
using Keryx.Domain.Sources;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Catalogue;

/// <summary>What the deduplicate and store stages counted.</summary>
public sealed class IngestionCounts
{
    public int Added { get; set; }

    public int Updated { get; set; }

    public int Unchanged { get; set; }

    public int Duplicates { get; set; }

    public int Rejected { get; set; }
}

/// <summary>
/// The deduplicate and store stages for one source's items (L2-009, L2-011, L2-015): each item is
/// bounded into an article's facts, matched by its canonical link against the articles Keryx holds
/// and the items this run already staged, counted, and staged for the publish stage.
/// </summary>
public sealed class ArticleIngestion(IAppDbContext db)
{
    public async Task StageAsync(
        Run run, Source source, IReadOnlyList<(FeedItem Item, decimal Score, PublicationStatus Status)> items, IngestionCounts counts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(counts);
        var seenHere = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (item, score, status) in items)
        {
            if (ItemUrl.Canonical(item.Link.OriginalString) is not { } url)
            {
                counts.Rejected++;
                continue;
            }

            if (!seenHere.Add(url))
            {
                // The same link twice in one feed is one story.
                counts.Duplicates++;
                continue;
            }

            var facts = FactsOf(item, url, source, score, status);
            var action = await MatchAsync(run, source, facts, counts, cancellationToken).ConfigureAwait(false);
            db.StagedArticles.Add(StagedArticle.Of(run.Id, action, source.Id, source.OrganizationName, facts));
        }
    }

    private async Task<StagedAction> MatchAsync(Run run, Source source, ArticleFacts facts, IngestionCounts counts, CancellationToken cancellationToken)
    {
        var known = await db.Articles.AsNoTracking()
            .Where(article => article.Url == facts.Url)
            .Select(article => new { article.Fingerprint, FromThisSource = article.Provenance.Any(entry => entry.SourceId == source.Id) })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (known is { FromThisSource: true })
        {
            if (known.Fingerprint == facts.Fingerprint)
            {
                counts.Unchanged++;
                return StagedAction.Seen;
            }

            counts.Updated++;
            return StagedAction.Update;
        }

        var stagedThisRun = await db.StagedArticles.AnyAsync(staged => staged.RunId == run.Id && staged.Url == facts.Url, cancellationToken).ConfigureAwait(false);
        if (known is not null || stagedThisRun)
        {
            // Another source's article, or another source's item this run: the same story (L2-010 AC1).
            counts.Duplicates++;
            return StagedAction.Seen;
        }

        counts.Added++;
        return StagedAction.Add;
    }

    private static ArticleFacts FactsOf(FeedItem item, string url, Source source, decimal score, PublicationStatus status)
    {
        var title = ArticleTitle.From(ContactDetailScrubber.Scrub(item.Title));
        var summary = ArticleSummary.From(ContactDetailScrubber.Scrub(item.Summary), null);
        IReadOnlyList<string> tags = TagSet.From([]);
        return new ArticleFacts(url, title, summary, ArticleCategory.For(title, summary, tags), source.RegionSlug, tags, item.PublishedAt, score, status);
    }
}

/// <summary>
/// The publish stage (L2-007): applies every change the run staged to the live articles in one
/// save, so the API serves the run's records all at once or not at all, then removes the staged
/// rows. Each source's entry moves to the run's start time.
/// </summary>
public sealed class ArticlePublication(IAppDbContext db)
{
    public async Task<int> PublishAsync(Run run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var staged = await db.StagedArticles.Where(row => row.RunId == run.Id).OrderBy(row => row.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var urls = staged.Select(row => row.Url).Distinct().ToList();
        var articles = await db.Articles.Where(article => urls.Contains(article.Url))
            .ToDictionaryAsync(article => article.Url, StringComparer.Ordinal, cancellationToken).ConfigureAwait(false);
        foreach (var row in staged)
        {
            if (!articles.TryGetValue(row.Url, out var article))
            {
                article = Article.Collect(row.Facts, row.SourceId, row.SourceName, run.StartedAt);
                articles[row.Url] = article;
                db.Articles.Add(article);
            }
            else
            {
                if (row.Action == StagedAction.Update)
                {
                    article.Apply(row.Facts);
                }

                article.Seen(row.SourceId, row.SourceName, row.Url, run.StartedAt);
            }
        }

        db.StagedArticles.RemoveRange(staged);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return staged.Count(row => row.Action != StagedAction.Seen);
    }
}
