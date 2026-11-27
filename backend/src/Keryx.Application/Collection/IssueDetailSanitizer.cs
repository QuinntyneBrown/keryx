using System.Text.RegularExpressions;

namespace Keryx.Application.Collection;

/// <summary>
/// Makes an issue's detail safe to store and show (L2-020 AC5): no user name or password in a
/// URL, no query string or fragment, no key, token or password value, no internal host name and
/// no IP address, and at most 300 characters.
/// </summary>
public static partial class IssueDetailSanitizer
{
    public const int MaxLength = 300;

    public static string Sanitize(string detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        var text = UserInfo().Replace(detail, "$1");
        text = QueryAndFragment().Replace(text, "$1");
        text = SecretValue().Replace(text, "$1[removed]");
        text = InternalHost().Replace(text, "[host]");
        text = IPv4().Replace(text, "[address]");
        text = IPv6().Replace(text, "[address]");
        return text.Length <= MaxLength ? text : string.Concat(text.AsSpan(0, MaxLength - 1), "…");
    }

    [GeneratedRegex(@"(https?://)[^/\s@]+@", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UserInfo();

    // Up to the end of the URL, leaving the sentence's closing punctuation.
    [GeneratedRegex(@"(https?://[^\s?#]*)[?#]\S*?(?=[.,;:)]*(?:\s|$))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex QueryAndFragment();

    [GeneratedRegex(@"(\b(?:api[_-]?key|access[_-]?token|token|secret|password|passwd|pwd)\s*[:=]\s*|\bBearer\s+)\S+?(?=[.,;]?(?:\s|$))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretValue();

    [GeneratedRegex(@"\b(?:localhost|[\w-]+(?:\.[\w-]+)*\.(?:internal|local|lan|localdomain|corp|home\.arpa))\b(?::\d+)?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InternalHost();

    [GeneratedRegex(@"\b(?:\d{1,3}\.){3}\d{1,3}(?::\d+)?\b", RegexOptions.CultureInvariant)]
    private static partial Regex IPv4();

    // Eight groups, or fewer with "::"; a time such as 12:02:33 is neither.
    [GeneratedRegex(@"(?<![\w:])(?=[0-9a-f:]*::|(?:[0-9a-f]{1,4}:){7})[0-9a-f]{0,4}(?::[0-9a-f]{0,4}){2,7}(?![\w:])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IPv6();
}
