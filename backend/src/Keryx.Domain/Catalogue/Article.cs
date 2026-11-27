using Keryx.Domain.Collection;

namespace Keryx.Domain.Catalogue;

/// <summary>
/// A pointer to a story a source published (L2-015): its bounded facts and canonical link, never
/// its text, images or HTML (L2-080). Its identity is its canonical link, so collecting it again
/// finds it whatever tracking a link carries (L2-009), and it keeps where each fact came from
/// (L2-011).
/// </summary>
public sealed class Article
{
    private readonly List<Provenance> _provenance = [];

    private Article()
    {
    }

    public long Id { get; private set; }

    /// <summary>The id the APIs show (<c>art_…</c>), drawn once and never changed.</summary>
    public string PublicId { get; private set; } = string.Empty;

    /// <summary>The canonical link to the source's own page: the article's identity.</summary>
    public string Url { get; private set; } = string.Empty;

    /// <summary>Null when the source gave none; the admin app shows "Untitled post".</summary>
    public string? Title { get; private set; }

    public string Summary { get; private set; } = string.Empty;

    /// <summary>The source the article was first collected from; null once that source is removed.</summary>
    public long? SourceId { get; private set; }

    /// <summary>The organization behind its first source; null when it cannot be determined (L2-015 AC4).</summary>
    public long? OrganizationId { get; private set; }

    public Organization? Organization { get; private set; }

    public string Category { get; private set; } = string.Empty;

    public string RegionSlug { get; private set; } = string.Empty;

    public IReadOnlyList<string> Tags { get; private set; } = [];

    /// <summary>When the source published it, in UTC, when the source said.</summary>
    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>When Keryx first collected it, in UTC.</summary>
    public DateTimeOffset CollectedAt { get; private set; }

    /// <summary>
    /// Where it sorts in a product's list, newest first: when the source published it, or, when the
    /// source gave no time, when Keryx first collected it (L2-026).
    /// </summary>
    public DateTimeOffset ListedAt { get; private set; }

    public decimal Score { get; private set; }

    public PublicationStatus Status { get; private set; }

    /// <summary>Why it has its status (<c>below_publish_threshold</c> for a held article); null when published.</summary>
    public string? StatusReason { get; private set; }

    /// <summary>The publish threshold in force when its status was set.</summary>
    public decimal Threshold { get; private set; }

    /// <summary>Who set its status: the run, from its score, or staff, whose decision a run never overwrites.</summary>
    public DecisionMaker DecidedBy { get; private set; }

    /// <summary>The status it held before staff last decided it; null when staff never did.</summary>
    public PublicationStatus? PreviousStatus { get; private set; }

    /// <summary>Who had set the status staff last replaced; null when staff never decided it.</summary>
    public DecisionMaker? PreviousDecidedBy { get; private set; }

    /// <summary>When staff last decided it, in UTC.</summary>
    public DateTimeOffset? DecidedAt { get; private set; }

    /// <summary>The staff session that made the last decision: only it may undo the decision (L2-014 AC4).</summary>
    public string? DecisionSession { get; private set; }

    /// <summary>A digest of the stored facts: an item collected again with the same digest is unchanged.</summary>
    public string Fingerprint { get; private set; } = string.Empty;

    public IReadOnlyList<Provenance> Provenance => _provenance;

    public static Article Collect(ArticleFacts facts, Organization? organization, long sourceId, string sourceName, DateTimeOffset seenAt)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var article = new Article
        {
            PublicId = PublicIds.New("art"),
            Url = facts.Url,
            SourceId = sourceId,
            Organization = organization,
            CollectedAt = seenAt,
        };
        article.Apply(facts);
        article._provenance.Add(new Provenance(sourceId, sourceName, facts.Url, seenAt));
        return article;
    }

    /// <summary>
    /// The source published new facts for it: they replace the old ones, with the score they
    /// earn. The status follows the score under the threshold of this run unless staff decided it.
    /// </summary>
    public void Apply(ArticleFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        Title = facts.Title;
        Summary = facts.Summary;
        Category = facts.Category;
        RegionSlug = facts.RegionSlug;
        Tags = facts.Tags;
        PublishedAt = facts.PublishedAt;
        ListedAt = facts.PublishedAt ?? CollectedAt;
        Score = facts.Score;
        Fingerprint = facts.Fingerprint;
        if (DecidedBy == DecisionMaker.System)
        {
            Status = facts.Status;
            StatusReason = PublicationPolicy.Reason(facts.Status);
            Threshold = facts.Threshold;
        }
    }

    /// <summary>
    /// Staff publish it whatever its score (L2-014 AC1). Returns false, changing nothing, when it is
    /// already published.
    /// </summary>
    public bool Publish(DateTimeOffset now, string session) => Decide(PublicationStatus.Published, now, session);

    /// <summary>
    /// Staff hide it from the consumer API whatever its score (L2-014 AC2). Returns false, changing
    /// nothing, when it is already hidden.
    /// </summary>
    public bool Hide(DateTimeOffset now, string session) => Decide(PublicationStatus.Hidden, now, session);

    /// <summary>
    /// Undoes the last decision (L2-014 AC4): the status it held and whoever had set it return. Only
    /// the session that decided may undo, only to the status the decision replaced, and only once;
    /// otherwise it returns false and changes nothing.
    /// </summary>
    public bool RevertTo(PublicationStatus previous, string session)
    {
        if (PreviousStatus != previous || PreviousDecidedBy is not { } decider || session.Length == 0 || DecisionSession != session)
        {
            return false;
        }

        Status = previous;
        DecidedBy = decider;
        StatusReason = decider == DecisionMaker.System ? PublicationPolicy.Reason(previous) : null;
        PreviousStatus = null;
        PreviousDecidedBy = null;
        DecisionSession = null;
        return true;
    }

    private bool Decide(PublicationStatus status, DateTimeOffset now, string session)
    {
        if (Status == status)
        {
            return false;
        }

        PreviousStatus = Status;
        PreviousDecidedBy = DecidedBy;
        Status = status;
        StatusReason = null;
        DecidedBy = DecisionMaker.Staff;
        DecidedAt = now;
        DecisionSession = session;
        return true;
    }

    /// <summary>The source was seen publishing it again: its entry's last-seen time moves, or a new source gets an entry.</summary>
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

/// <summary>Who set an article's status.</summary>
public enum DecisionMaker
{
    System,
    Staff,
}

/// <summary>
/// Where a record's facts came from (L2-011): the source, a snapshot of its name and the URL read,
/// so the entry stays readable after the source is removed, and when it was first and last seen.
/// </summary>
public sealed class Provenance
{
    internal Provenance(long sourceId, string sourceName, string url, DateTimeOffset seenAt)
    {
        SourceId = sourceId;
        SourceName = sourceName;
        Url = url;
        FirstSeenAt = seenAt;
        LastSeenAt = seenAt;
    }

    private Provenance()
    {
    }

    /// <summary>Null once the source is removed; the name and URL stay.</summary>
    public long? SourceId { get; private set; }

    public string SourceName { get; private set; } = string.Empty;

    public string Url { get; private set; } = string.Empty;

    public DateTimeOffset FirstSeenAt { get; private set; }

    public DateTimeOffset LastSeenAt { get; private set; }

    internal void SeenAgain(DateTimeOffset seenAt) => LastSeenAt = seenAt;
}

/// <summary>The bounded facts of one collected item, ready to store (L2-015).</summary>
public sealed record ArticleFacts(
    string Url,
    string? Title,
    string Summary,
    string Category,
    string RegionSlug,
    IReadOnlyList<string> Tags,
    DateTimeOffset? PublishedAt,
    decimal Score,
    PublicationStatus Status)
{
    /// <summary>The publish threshold of the run that collected it.</summary>
    public decimal Threshold { get; init; }

    /// <summary>The item's event as the source published it (start, end and place), or empty when it is no event.</summary>
    public string EventSignature { get; init; } = string.Empty;

    /// <summary>
    /// The facts a source controls, its event included. Score and status follow from them under
    /// the settings of the run, so a settings change alone does not make every article an update.
    /// </summary>
    public string Fingerprint => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
        string.Join('\u001f', Title ?? string.Empty, Summary, string.Join(',', Tags), EventSignature,
            PublishedAt?.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty))));
}
