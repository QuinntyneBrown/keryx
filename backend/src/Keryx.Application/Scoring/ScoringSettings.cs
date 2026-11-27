using Keryx.Domain;

namespace Keryx.Application.Scoring;

/// <summary>The part of Settings that scoring reads, taken once so a run scores with one set (L2-012 AC5).</summary>
public sealed record ScoringSettings(
    IReadOnlyList<string> Topics,
    IReadOnlyList<string> Exclusions,
    IReadOnlyList<string> FocusAreas,
    bool IncludeOntarioWide,
    bool IncludeNational)
{
    public static ScoringSettings From(Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new([.. settings.Topics], [.. settings.Exclusions], [.. settings.FocusAreas], settings.IncludeOntarioWide, settings.IncludeNational);
    }
}

/// <summary>What scoring reads of an item: its words and where it is, as the source published it.</summary>
public sealed record ScoringItem(string Title, string Summary, string Place);
