using Keryx.Domain.Sources;

namespace Keryx.Application.Sources;

/// <summary>Reads the items out of what a source returned. Infrastructure implements it per kind.</summary>
public interface IFeedParser
{
    /// <summary>The items, or null when the content is not what the kind expects.</summary>
    ParsedFeed? Parse(SourceKind kind, string content);
}

/// <summary>An item as a source published it: plain text, and a link that is http or https.</summary>
public sealed record FeedItem(string Title, Uri Link, string Summary, DateTimeOffset? PublishedAt, ItemEvent? Event = null);

/// <summary>When and where an item's event happens, as text the source published (RSS event module); bounded plain text.</summary>
public sealed record ItemEvent(string Start, string End, string Place);

/// <summary>The items kept (at most 200, newest first) and how many were rejected.</summary>
public sealed record ParsedFeed(IReadOnlyList<FeedItem> Items, int Rejected);
