using Keryx.Domain.Collection;

namespace Keryx.Domain.Catalogue;

/// <summary>
/// Something with a start time and a place: a race, a class, a camp, a service project (L2-016).
/// Its start is an instant in UTC with the IANA zone it is shown in; it belongs to an
/// organization, links to the article it came from, and keeps where it was seen (L2-011).
/// </summary>
public sealed class EventRecord
{
    private readonly List<Provenance> _provenance = [];

    private EventRecord()
    {
    }

    public long Id { get; private set; }

    /// <summary>The id the APIs show (<c>evt_…</c>), drawn once and never changed.</summary>
    public string PublicId { get; private set; } = string.Empty;

    public string? Title { get; private set; }

    public DateTimeOffset StartsAtUtc { get; private set; }

    /// <summary>An IANA zone (<c>America/Toronto</c>).</summary>
    public string TimeZone { get; private set; } = string.Empty;

    /// <summary>Null when the source gave none, or gave one before the start.</summary>
    public DateTimeOffset? EndsAtUtc { get; private set; }

    public string PlaceName { get; private set; } = string.Empty;

    /// <summary>The place's match key: case, punctuation and whitespace ignored.</summary>
    public string PlaceKey { get; private set; } = string.Empty;

    public string RegionSlug { get; private set; } = string.Empty;

    public long? OrganizationId { get; private set; }

    public Organization? Organization { get; private set; }

    public long? LinkedArticleId { get; private set; }

    /// <summary>The article it came from; the event is served only while that article is published.</summary>
    public Article? LinkedArticle { get; private set; }

    public decimal Score { get; private set; }

    public PublicationStatus Status { get; private set; }

    public IReadOnlyList<Provenance> Provenance => _provenance;

    public static EventRecord From(EventFacts facts, Organization? organization, Article article, long sourceId, string sourceName, DateTimeOffset seenAt)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(article);
        var item = new EventRecord { PublicId = PublicIds.New("evt"), Organization = organization, LinkedArticle = article };
        item.Apply(facts);
        item._provenance.Add(new Provenance(sourceId, sourceName, facts.Url, seenAt));
        return item;
    }

    /// <summary>The source published new facts for it: they replace the old ones.</summary>
    public void Apply(EventFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        Title = facts.Title;
        StartsAtUtc = facts.Start.StartsAtUtc;
        TimeZone = facts.Start.TimeZone;
        EndsAtUtc = facts.EndsAtUtc is { } end && end >= facts.Start.StartsAtUtc ? end : null;
        PlaceName = facts.PlaceName;
        PlaceKey = EventMatch.PlaceKey(facts.PlaceName);
        RegionSlug = facts.RegionSlug;
        Score = facts.Score;
        Status = facts.Status;
    }

    /// <summary>A source was seen publishing it: its entry moves, or the source gets one.</summary>
    public void Seen(long sourceId, string sourceName, string url, DateTimeOffset seenAt)
    {
        var entry = _provenance.FirstOrDefault(candidate => candidate.SourceId == sourceId);
        if (entry is null)
        {
            _provenance.Add(new Provenance(sourceId, sourceName, url, seenAt));
        }
        else
        {
            entry.SeenAgain(seenAt);
        }
    }
}

/// <summary>The facts of one event, read from an item that published a start time and a place.</summary>
public sealed record EventFacts(
    string Url, string? Title, EventTime Start, DateTimeOffset? EndsAtUtc, string PlaceName, string RegionSlug, decimal Score, PublicationStatus Status);
