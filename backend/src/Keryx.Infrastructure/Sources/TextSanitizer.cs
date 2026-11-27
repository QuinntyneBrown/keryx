using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Keryx.Infrastructure.Sources;

/// <summary>
/// Turns a title or summary a source published into plain text (L2-023): markup removed,
/// entities decoded, control characters dropped and whitespace collapsed, so a stored value
/// holds no tag.
/// </summary>
internal static partial class TextSanitizer
{
    public static string PlainText(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var text = WebUtility.HtmlDecode(Tags().Replace(value, " "));
        // A second pass removes markup that was escaped as entities in the source.
        text = Tags().Replace(text, " ");
        var plain = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            plain.Append(char.IsControl(character) ? ' ' : character);
        }

        var collapsed = Whitespace().Replace(plain.ToString(), " ").Trim();
        return collapsed.Length <= maxLength ? collapsed : collapsed[..maxLength].TrimEnd();
    }

    [GeneratedRegex("<[^>]*>", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Whitespace();
}
