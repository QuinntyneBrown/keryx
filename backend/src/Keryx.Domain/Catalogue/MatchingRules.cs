using System.Text;

namespace Keryx.Domain.Catalogue;

/// <summary>
/// The canonical form of an item's link, used to match an item collected again (L2-009 AC3):
/// an absolute http(s) URL with the scheme and host lower-cased, the default port, a trailing
/// slash, the fragment and the tracking parameters (<c>utm_*</c>, <c>fbclid</c>, <c>gclid</c>)
/// removed. The path and the other parameters keep their case and order.
/// </summary>
public static class ItemUrl
{
    public static string? Canonical(string? url)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) || uri.Host.Length == 0)
        {
            return null;
        }

        var kept = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(pair => !IsTracking(pair.Split('=', 2)[0]))
            .ToList();
        var port = uri.IsDefaultPort ? string.Empty : ":" + uri.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var query = kept.Count == 0 ? string.Empty : "?" + string.Join('&', kept);
        return $"{uri.Scheme}://{uri.Host.ToLowerInvariant()}{port}{uri.AbsolutePath.TrimEnd('/')}{query}";
    }

    private static bool IsTracking(string name) =>
        name.StartsWith("utm_", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("fbclid", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("gclid", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// How alike two headlines are (L2-010 AC2): one minus the word-level edit distance divided by
/// the longer headline's word count, to two decimals, after case, punctuation and whitespace are
/// ignored. One word changed in ten scores exactly 0.90, and items merge at 0.90 inclusive.
/// </summary>
public static class HeadlineSimilarity
{
    public const decimal MergeAt = 0.90m;

    public static bool Merges(string first, string second) => Score(first, second) >= MergeAt;

    public static decimal Score(string first, string second)
    {
        var a = Words(first);
        var b = Words(second);
        var longer = Math.Max(a.Length, b.Length);
        if (longer == 0)
        {
            return 0m;
        }

        return Math.Round(1m - ((decimal)Distance(a, b) / longer), 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>The words of a headline: lower-cased, with punctuation and whitespace as separators.</summary>
    public static string[] Words(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var plain = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            plain.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : ' ');
        }

        return plain.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private static int Distance(string[] a, string[] b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var substitution = previous[j - 1] + (string.Equals(a[i - 1], b[j - 1], StringComparison.Ordinal) ? 0 : 1);
                current[j] = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), substitution);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}

/// <summary>
/// The key that makes two sources' names for one organization the same (L2-010 AC5): case,
/// punctuation and whitespace ignored, and a leading "The" dropped.
/// </summary>
public static class OrganizationName
{
    public static string Normalize(string name)
    {
        var words = HeadlineSimilarity.Words(name);
        var start = words.Length > 1 && words[0] == "the" ? 1 : 0;
        return string.Join(' ', words[start..]);
    }
}
