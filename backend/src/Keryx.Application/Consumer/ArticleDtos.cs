namespace Keryx.Application.Consumer;

/// <summary>The organization an article is from.</summary>
public sealed record OrganizationRefDto(string Id, string Name);

/// <summary>A source an article was read from: its name and the page's URL.</summary>
public sealed record SourceLinkDto(string Name, string Url);

/// <summary>
/// One article as a product reads it (L2-027): the list fields, its tags, when Keryx collected
/// it, the event it announces, and where it was read. No score, status or internal field.
/// </summary>
public sealed record ArticleDetailDto(
    string Id,
    string Title,
    OrganizationRefDto? Organization,
    string Category,
    string Region,
    DateTimeOffset? PublishedAt,
    string Url,
    string Summary,
    IReadOnlyList<string> Tags,
    DateTimeOffset CollectedAt,
    string? EventId,
    IReadOnlyList<SourceLinkDto> Sources);
