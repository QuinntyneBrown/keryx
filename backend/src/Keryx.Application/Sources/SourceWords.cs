using Keryx.Domain.Sources;

namespace Keryx.Application.Sources;

/// <summary>The words the API uses for a source's kind and health (L2-001, L2-004).</summary>
public static class SourceWords
{
    public static string Kind(SourceKind kind) => kind switch
    {
        SourceKind.Rss => "rss",
        SourceKind.Website => "website",
        SourceKind.Instagram => "instagram",
        SourceKind.Eventbrite => "eventbrite",
        SourceKind.NewsQuery => "news_query",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown source kind."),
    };

    /// <summary>The kind a word names, or null for a word that names none.</summary>
    public static SourceKind? KindOf(string? word) => word switch
    {
        "rss" => SourceKind.Rss,
        "website" => SourceKind.Website,
        "instagram" => SourceKind.Instagram,
        "eventbrite" => SourceKind.Eventbrite,
        "news_query" => SourceKind.NewsQuery,
        _ => null,
    };

    public static string Health(SourceHealth health) => health switch
    {
        SourceHealth.Healthy => "healthy",
        SourceHealth.NeedsLook => "needs_look",
        SourceHealth.Failing => "failing",
        _ => throw new ArgumentOutOfRangeException(nameof(health), health, "Unknown source health."),
    };

    /// <summary>The health a word names, or null for a word that names none.</summary>
    public static SourceHealth? HealthOf(string? word) => word switch
    {
        "healthy" => SourceHealth.Healthy,
        "needs_look" => SourceHealth.NeedsLook,
        "failing" => SourceHealth.Failing,
        _ => null,
    };
}
