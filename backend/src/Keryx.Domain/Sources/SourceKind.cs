namespace Keryx.Domain.Sources;

/// <summary>What a source is, and so how it is read (L2-001).</summary>
public enum SourceKind
{
    Rss,
    Website,
    Instagram,
    Eventbrite,

    /// <summary>A search of the news, by query text. Created by seed data only.</summary>
    NewsQuery,
}
