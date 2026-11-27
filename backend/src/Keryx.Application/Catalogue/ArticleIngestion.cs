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
/// The deduplicate and store stages for one source's items (L2-009, L2-010, L2-011, L2-015): each
/// item is bounded into an article's facts, matched against the articles Keryx holds and the
/// items this run already staged, counted, and staged for the publish stage.
/// </summary>
public sealed class ArticleIngestion(IAppDbContext db)
{
    /// <summary>How far back a headline of the same organization is compared, on the time it was collected (L2-010).</summary>
    public static readonly TimeSpan MergeWindow = TimeSpan.FromDays(14);

    public async Task StageAsync(
        Run run, Source source, IReadOnlyList<(FeedItem Item, decimal Score, PublicationStatus Status)> items, bool merge, IngestionCounts counts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(counts);
        var seenHere = new HashSet<string>(StringComparer.Ordinal);
        string? zone = null;
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

            // An item is also an event when it publishes a readable start and a place; a local time takes the region's zone (L2-018).
            (EventTime Start, EventTime? End, string Place)? itemEvent = null;
            if (item.Event is { Place.Length: > 0 } published)
            {
                zone ??= await db.Regions.Where(region => region.Slug == source.RegionSlug).Select(region => region.TimeZone)
                    .SingleAsync(cancellationToken).ConfigureAwait(false);
                if (EventTime.From(published.Start, zone) is { } start)
                {
                    itemEvent = (start, EventTime.From(published.End, zone), ContactDetailScrubber.Scrub(published.Place));
                }
            }

            var facts = FactsOf(item, url, source, score, status) with
            {
                EventSignature = itemEvent is { } known ? $"{known.Start.StartsAtUtc:O}|{known.End?.StartsAtUtc:O}|{known.Place}" : string.Empty,
            };
            var (action, target) = await MatchAsync(run, source, facts, merge, counts, cancellationToken).ConfigureAwait(false);
            var row = StagedArticle.Of(run.Id, action, target, source.Id, source.OrganizationName, facts);
            if (itemEvent is { } found)
            {
                row.WithEvent(found.Start, found.End, found.Place);
            }

            db.StagedArticles.Add(row);
        }
    }

    private async Task<(StagedAction Action, string? Target)> MatchAsync(
        Run run, Source source, ArticleFacts facts, bool merge, IngestionCounts counts, CancellationToken cancellationToken)
    {
        // Its identity: this source has published this link before (L2-009).
        var known = await db.Articles.AsNoTracking()
            .Where(article => article.Provenance.Any(entry => entry.SourceId == source.Id && entry.Url == facts.Url))
            .Select(article => new { article.Url, article.SourceId, article.Fingerprint })
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (known is not null)
        {
            // Only the article's own source changes its facts; a merged item seen again is unchanged.
            if (known.SourceId == source.Id && known.Fingerprint != facts.Fingerprint)
            {
                counts.Updated++;
                return (StagedAction.Update, null);
            }

            counts.Unchanged++;
            return (StagedAction.Seen, Other(known.Url, facts));
        }

        // The same link from another source, held or staged this run, is the same story whatever the setting (L2-010 AC1, AC3).
        var sameLink = await db.Articles.AnyAsync(article => article.Url == facts.Url, cancellationToken).ConfigureAwait(false) ||
            await db.StagedArticles.AnyAsync(staged => staged.RunId == run.Id && staged.Url == facts.Url && staged.Action == StagedAction.Add, cancellationToken).ConfigureAwait(false);
        if (sameLink)
        {
            counts.Duplicates++;
            return (StagedAction.Seen, null);
        }

        if (merge && await AlikeHeadlineAsync(run, source, facts, cancellationToken).ConfigureAwait(false) is { } alike)
        {
            counts.Duplicates++;
            return (StagedAction.Seen, alike);
        }

        counts.Added++;
        return (StagedAction.Add, null);
    }

    /// <summary>
    /// The link of an article of the same organization, collected within the last 14 days or
    /// staged this run, whose headline is at least 0.90 alike (L2-010 AC2); null when there is none.
    /// </summary>
    private async Task<string?> AlikeHeadlineAsync(Run run, Source source, ArticleFacts facts, CancellationToken cancellationToken)
    {
        var key = OrganizationName.Normalize(source.OrganizationName);
        if (key.Length == 0 || facts.Title is null)
        {
            return null;
        }

        var since = run.StartedAt - MergeWindow;
        var held = await db.Articles.AsNoTracking()
            .Where(article => article.Organization != null && article.Organization.NormalizedName == key && article.CollectedAt >= since && article.Title != null)
            .OrderBy(article => article.Id)
            .Select(article => new { article.Url, Title = article.Title! })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var staged = await db.StagedArticles.AsNoTracking()
            .Where(row => row.RunId == run.Id && row.OrganizationKey == key && row.Action == StagedAction.Add && row.Title != null)
            .OrderBy(row => row.Id)
            .Select(row => new { row.Url, Title = row.Title! })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return held.Concat(staged).FirstOrDefault(candidate => HeadlineSimilarity.Merges(candidate.Title, facts.Title))?.Url;
    }

    private static string? Other(string articleUrl, ArticleFacts facts) =>
        string.Equals(articleUrl, facts.Url, StringComparison.Ordinal) ? null : articleUrl;

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
/// rows. Each source's entry moves to the run's start time. The organization behind each source
/// is found by its normalized name or created (L2-010 AC5, L2-017); sources are taken in the
/// order they were added, so the first source to name an organization gives its name.
/// </summary>
public sealed class ArticlePublication(IAppDbContext db)
{
    public async Task<int> PublishAsync(Run run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var staged = await db.StagedArticles.Where(row => row.RunId == run.Id).OrderBy(row => row.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var urls = staged.Select(row => row.TargetUrl ?? row.Url).Distinct().ToList();
        var articles = await db.Articles.Where(article => urls.Contains(article.Url))
            .ToDictionaryAsync(article => article.Url, StringComparer.Ordinal, cancellationToken).ConfigureAwait(false);
        var organizations = await OrganizationsAsync(run, staged, cancellationToken).ConfigureAwait(false);
        var events = new List<EventRecord>();
        foreach (var row in staged)
        {
            if (!articles.TryGetValue(row.TargetUrl ?? row.Url, out var article))
            {
                article = Article.Collect(row.Facts, organizations.GetValueOrDefault(row.SourceId), row.SourceId, row.SourceName, run.StartedAt);
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

            if (row.EventFacts is { } facts)
            {
                await PublishEventAsync(run, row, facts, article, organizations.GetValueOrDefault(row.SourceId), events, cancellationToken).ConfigureAwait(false);
            }
        }

        db.StagedArticles.RemoveRange(staged);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return staged.Count(row => row.Action != StagedAction.Seen);
    }

    /// <summary>
    /// An item's event: the event already linked to its article (the same item collected again),
    /// else an event of the same organization within an hour at the same place (L2-010 AC4), else
    /// a new event linked to the article.
    /// </summary>
    private async Task PublishEventAsync(
        Run run, StagedArticle row, EventFacts facts, Article article, Organization? organization, List<EventRecord> created, CancellationToken cancellationToken)
    {
        var item = article.Id == 0 ? null : await db.Events.FirstOrDefaultAsync(candidate => candidate.LinkedArticleId == article.Id, cancellationToken).ConfigureAwait(false);
        if (item is not null && row.TargetUrl is null && row.Action != StagedAction.Seen)
        {
            item.Apply(facts);
        }

        if (item is null && organization is not null)
        {
            var from = facts.Start.StartsAtUtc - EventMatch.Window;
            var to = facts.Start.StartsAtUtc + EventMatch.Window;
            var nearby = organization.Id == 0 ? [] : await db.Events
                .Where(candidate => candidate.OrganizationId == organization.Id && candidate.StartsAtUtc >= from && candidate.StartsAtUtc <= to)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            item = nearby.Concat(created.Where(candidate => candidate.Organization == organization))
                .FirstOrDefault(candidate => EventMatch.Same(candidate.StartsAtUtc, candidate.PlaceName, facts.Start.StartsAtUtc, facts.PlaceName));
        }

        if (item is null)
        {
            item = EventRecord.From(facts, organization, article, row.SourceId, row.SourceName, run.StartedAt);
            created.Add(item);
            db.Events.Add(item);
            return;
        }

        item.Seen(row.SourceId, row.SourceName, row.Url, run.StartedAt);
    }

    /// <summary>The organization behind each source with staged items, found or created, and seen by that source.</summary>
    private async Task<Dictionary<long, Organization>> OrganizationsAsync(Run run, List<StagedArticle> staged, CancellationToken cancellationToken)
    {
        var sourceIds = staged.Select(row => row.SourceId).Distinct().ToList();
        var sources = await db.Sources.AsNoTracking().Where(source => sourceIds.Contains(source.Id)).OrderBy(source => source.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var keys = sources.Select(source => OrganizationName.Normalize(source.OrganizationName)).Distinct().ToList();
        var known = await db.Organizations.Where(organization => keys.Contains(organization.NormalizedName))
            .ToDictionaryAsync(organization => organization.NormalizedName, StringComparer.Ordinal, cancellationToken).ConfigureAwait(false);
        var bySource = new Dictionary<long, Organization>();
        foreach (var source in sources)
        {
            var key = OrganizationName.Normalize(source.OrganizationName);
            if (key.Length == 0)
            {
                continue;
            }

            if (known.TryGetValue(key, out var organization))
            {
                organization.Seen(source.Id, source.OrganizationName, source.Address, run.StartedAt);
            }
            else
            {
                organization = Organization.Named(source.OrganizationName, source.RegionSlug, source.Address, source.Id, source.OrganizationName, run.StartedAt);
                known[key] = organization;
                db.Organizations.Add(organization);
            }

            bySource[source.Id] = organization;
        }

        return bySource;
    }
}
