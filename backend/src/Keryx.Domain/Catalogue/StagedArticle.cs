using Keryx.Domain.Collection;

namespace Keryx.Domain.Catalogue;

/// <summary>What the store stage decided for one item.</summary>
public enum StagedAction
{
    /// <summary>A new article.</summary>
    Add,

    /// <summary>A known article whose facts changed at the source.</summary>
    Update,

    /// <summary>A known article seen again unchanged, or the same story from another source.</summary>
    Seen,
}

/// <summary>
/// A change a run made to an article, held until the run's publish stage applies every change in
/// one save (L2-007). Until then the API serves what it served before the run; a run whose store
/// stage fails discards its staged rows.
/// </summary>
public sealed class StagedArticle
{
    private StagedArticle()
    {
    }

    public long Id { get; private set; }

    public long RunId { get; private set; }

    public StagedAction Action { get; private set; }

    public long SourceId { get; private set; }

    public string SourceName { get; private set; } = string.Empty;

    /// <summary>The item's canonical link: the URL its facts were read from.</summary>
    public string Url { get; private set; } = string.Empty;

    /// <summary>The link of the article the item merges into, when that is another article's (L2-010); null otherwise.</summary>
    public string? TargetUrl { get; private set; }

    /// <summary>The match key of the organization behind the source; empty when there is none.</summary>
    public string OrganizationKey { get; private set; } = string.Empty;

    public string? Title { get; private set; }

    public string Summary { get; private set; } = string.Empty;

    public string Category { get; private set; } = string.Empty;

    public string RegionSlug { get; private set; } = string.Empty;

    public IReadOnlyList<string> Tags { get; private set; } = [];

    public DateTimeOffset? PublishedAt { get; private set; }

    public decimal Score { get; private set; }

    public decimal Threshold { get; private set; }

    /// <summary>When the item's event starts, in UTC; null when the item is no event (it needs a start and a place).</summary>
    public DateTimeOffset? EventStartsAtUtc { get; private set; }

    public string? EventTimeZone { get; private set; }

    public DateTimeOffset? EventEndsAtUtc { get; private set; }

    public string? EventPlace { get; private set; }

    /// <summary>The item's event as published, part of the facts' fingerprint.</summary>
    public string EventSignature { get; private set; } = string.Empty;

    public PublicationStatus Status { get; private set; }

    public ArticleFacts Facts => new(Url, Title, Summary, Category, RegionSlug, Tags, PublishedAt, Score, Status) { EventSignature = EventSignature, Threshold = Threshold };

    /// <summary>The item's event, when it is one.</summary>
    public EventFacts? EventFacts => EventStartsAtUtc is { } start && EventTimeZone is { } zone && EventPlace is { } place
        ? new EventFacts(Url, Title, new EventTime(start, zone), EventEndsAtUtc, place, RegionSlug, Score, Status)
        : null;

    /// <summary>The same row, carrying the item's event.</summary>
    public StagedArticle WithEvent(EventTime start, EventTime? end, string place)
    {
        ArgumentNullException.ThrowIfNull(start);
        EventStartsAtUtc = start.StartsAtUtc;
        EventTimeZone = start.TimeZone;
        EventEndsAtUtc = end?.StartsAtUtc;
        EventPlace = place;
        return this;
    }

    public static StagedArticle Of(long runId, StagedAction action, string? targetUrl, long sourceId, string sourceName, ArticleFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return new StagedArticle
        {
            RunId = runId,
            Action = action,
            TargetUrl = targetUrl,
            OrganizationKey = OrganizationName.Normalize(sourceName),
            SourceId = sourceId,
            SourceName = sourceName,
            Url = facts.Url,
            Title = facts.Title,
            Summary = facts.Summary,
            Category = facts.Category,
            RegionSlug = facts.RegionSlug,
            Tags = facts.Tags,
            PublishedAt = facts.PublishedAt,
            Score = facts.Score,
            Status = facts.Status,
            EventSignature = facts.EventSignature,
            Threshold = facts.Threshold,
        };
    }
}
