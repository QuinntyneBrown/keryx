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

    public string Url { get; private set; } = string.Empty;

    public string? Title { get; private set; }

    public string Summary { get; private set; } = string.Empty;

    public string Category { get; private set; } = string.Empty;

    public string RegionSlug { get; private set; } = string.Empty;

    public IReadOnlyList<string> Tags { get; private set; } = [];

    public DateTimeOffset? PublishedAt { get; private set; }

    public decimal Score { get; private set; }

    public PublicationStatus Status { get; private set; }

    public ArticleFacts Facts => new(Url, Title, Summary, Category, RegionSlug, Tags, PublishedAt, Score, Status);

    public static StagedArticle Of(long runId, StagedAction action, long sourceId, string sourceName, ArticleFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return new StagedArticle
        {
            RunId = runId,
            Action = action,
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
        };
    }
}
