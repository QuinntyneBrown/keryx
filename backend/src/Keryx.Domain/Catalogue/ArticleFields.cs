using System.Text.RegularExpressions;

namespace Keryx.Domain.Catalogue;

/// <summary>Cuts text to a length on a word boundary, ending with an ellipsis when anything was cut.</summary>
internal static class Bounded
{
    public static string ToWord(string text, int max)
    {
        if (text.Length <= max)
        {
            return text;
        }

        var cut = text[..(max - 1)];
        var space = cut.LastIndexOf(' ');
        return (space > 0 ? cut[..space] : cut).TrimEnd(' ', ',', ';', ':', '-') + "…";
    }
}

/// <summary>An article's title as the source published it, whole up to 300 characters (L2-015 AC1, AC3).</summary>
public static class ArticleTitle
{
    public const int MaxLength = 300;

    /// <summary>The title, cut on a word with an ellipsis past 300 characters; null when there is none.</summary>
    public static string? From(string? title) =>
        string.IsNullOrWhiteSpace(title) ? null : Bounded.ToWord(title.Trim(), MaxLength);
}

/// <summary>
/// An article's summary: the source's own, or with none the item's first sentence, at most 500
/// characters ending on a word (L2-015 AC1, AC2). No more of the item's text is kept.
/// </summary>
public static partial class ArticleSummary
{
    public const int MaxLength = 500;

    public static string From(string? summary, string? text)
    {
        var chosen = !string.IsNullOrWhiteSpace(summary) ? summary.Trim() : FirstSentence(text);
        return Bounded.ToWord(chosen, MaxLength);
    }

    /// <summary>The text up to and including its first full stop, question or exclamation mark.</summary>
    public static string FirstSentence(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var match = SentenceEnd().Match(text);
        return (match.Success ? text[..(match.Index + 1)] : text).Trim();
    }

    [GeneratedRegex(@"[.!?](?=\s|$)", RegexOptions.CultureInvariant)]
    private static partial Regex SentenceEnd();
}

/// <summary>
/// Removes e-mail addresses and phone numbers from a title or summary before it is stored
/// (L2-081 AC3). A name stays inside the text, and nothing is extracted from it.
/// </summary>
public static partial class ContactDetailScrubber
{
    public static string Scrub(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var scrubbed = Phone().Replace(Email().Replace(text, string.Empty), string.Empty);
        scrubbed = SpaceBeforePunctuation().Replace(Spaces().Replace(scrubbed, " "), "$1");
        return scrubbed.Trim();
    }

    [GeneratedRegex(@"[\w.+-]+@[\w-]+(?:\.[\w-]+)+", RegexOptions.CultureInvariant)]
    private static partial Regex Email();

    [GeneratedRegex(@"(?:\+?1[\s.-]?)?(?:\(\d{3}\)|\b\d{3})[\s.-]?\d{3}[\s.-]?\d{4}\b", RegexOptions.CultureInvariant)]
    private static partial Regex Phone();

    [GeneratedRegex(@"\s{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\s+([.,;:!?])", RegexOptions.CultureInvariant)]
    private static partial Regex SpaceBeforePunctuation();
}

/// <summary>An article's tags: the source's own, lower-cased and de-duplicated, at most 30 of at most 40 characters (L2-015 AC6).</summary>
public static class TagSet
{
    public const int MaxTags = 30;
    public const int MaxTagLength = 40;

    /// <summary>The tags in their first order. A tag longer than 40 characters is dropped, not cut.</summary>
    public static IReadOnlyList<string> From(IEnumerable<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        return [.. tags
            .Select(tag => tag.Trim().ToLowerInvariant())
            .Where(tag => tag.Length is > 0 and <= MaxTagLength)
            .Distinct(StringComparer.Ordinal)
            .Take(MaxTags)];
    }
}

/// <summary>
/// The one category an article gets (L2-015 AC5). A source tag naming a category decides it;
/// otherwise the first category whose words the title or summary mentions, in the order below,
/// and <c>faith-wellness</c> when none does.
/// </summary>
public static partial class ArticleCategory
{
    public const string Default = "faith-wellness";

    private static readonly (string Slug, string Name, Regex Words)[] Rules =
    [
        ("races-challenges", "Races & Challenges", Races()),
        ("events", "Events", Events()),
        ("gyms-studios", "Gyms & Studios", Gyms()),
        ("nutrition", "Nutrition", Nutrition()),
        ("ministry-programs", "Ministry Programs", Ministry()),
        ("faith-wellness", "Faith & Wellness", Faith()),
    ];

    public static string For(string? title, string? summary, IEnumerable<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        foreach (var tag in tags.Select(tag => tag.Trim()))
        {
            var named = Rules.FirstOrDefault(rule =>
                string.Equals(tag, rule.Slug, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag, rule.Name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag, rule.Name.Replace("&", "and", StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase));
            if (named.Slug is not null)
            {
                return named.Slug;
            }
        }

        var text = $"{title}\n{summary}";
        return Rules.FirstOrDefault(rule => rule.Words.IsMatch(text)).Slug ?? Default;
    }

    [GeneratedRegex(@"\b(?:race|races|5k|10k|half[- ]marathon|marathon|triathlon|challenge|fun run|relay)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Races();

    [GeneratedRegex(@"\b(?:event|events|camp|retreat|workshop|conference|class|classes|festival|clinic)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Events();

    [GeneratedRegex(@"\b(?:gym|gyms|studio|studios|crossfit|bootcamp|fitness centre|fitness center)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Gyms();

    [GeneratedRegex(@"\b(?:nutrition|meal|meals|diet|recipe|recipes|food|healthy eating)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Nutrition();

    [GeneratedRegex(@"\b(?:ministry|ministries|outreach|program|programs|small group|youth group)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Ministry();

    [GeneratedRegex(@"\b(?:faith|prayer|wellness|devotional)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Faith();
}
