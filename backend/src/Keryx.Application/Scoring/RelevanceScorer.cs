using System.Text.RegularExpressions;

namespace Keryx.Application.Scoring;

/// <summary>Scores an item's relevance (L2-012). Deterministic: the same item and settings give the same score.</summary>
public interface IRelevanceScorer
{
    RelevanceScore Score(ScoringItem item, ScoringSettings settings);
}

/// <summary>
/// The scoring rule. An item matching an exclusion scores 0.10. Otherwise its score is
/// 0.6 × its topic share (distinct topics matched, at most 3, over 3) plus 0.4 × its geography,
/// and an item matching no topic scores 0.3 × its geography, so it stays below the review floor
/// of 0.45. Geography is 1.0 in a focus area, 0.6 in Ontario with "Ontario-wide organisations"
/// on, 0.5 in Canada with "National Canadian news" on, and 0.2 anywhere else. Words match whole
/// and without regard to case.
/// </summary>
public sealed class RelevanceScorer : IRelevanceScorer
{
    private const decimal Excluded = 0.10m;
    private const int TopicsForFullShare = 3;

    public RelevanceScore Score(ScoringItem item, ScoringSettings settings)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(settings);
        var text = $"{item.Title}\n{item.Summary}";
        if (settings.Exclusions.Any(exclusion => Mentions(text, exclusion)))
        {
            return RelevanceScore.Of(Excluded);
        }

        var topics = settings.Topics.Distinct(StringComparer.OrdinalIgnoreCase).Count(topic => Mentions(text, topic));
        var geography = Geography(item, text, settings);
        if (topics == 0)
        {
            return RelevanceScore.Of(0.3m * geography);
        }

        var share = (decimal)Math.Min(topics, TopicsForFullShare) / TopicsForFullShare;
        return RelevanceScore.Of((0.6m * share) + (0.4m * geography));
    }

    private static decimal Geography(ScoringItem item, string text, ScoringSettings settings)
    {
        if (settings.FocusAreas.Any(area => Mentions(item.Place, area) || Mentions(text, area)))
        {
            return 1.0m;
        }

        if (settings.IncludeOntarioWide && (Mentions(item.Place, "Ontario") || Regex.IsMatch(item.Place, @",\s*ON\b", RegexOptions.CultureInvariant)))
        {
            return 0.6m;
        }

        return settings.IncludeNational && Mentions(item.Place, "Canada") ? 0.5m : 0.2m;
    }

    private static bool Mentions(string text, string phrase) =>
        phrase.Length > 0
        && Regex.IsMatch(text, $@"(?<!\w){Regex.Escape(phrase)}(?!\w)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
}
