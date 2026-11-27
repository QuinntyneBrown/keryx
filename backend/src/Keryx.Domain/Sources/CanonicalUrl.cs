namespace Keryx.Domain.Sources;

/// <summary>
/// The form of an address that decides whether two sources are the same (L2-001 AC4): scheme and
/// host lower-cased, the scheme's default port, a trailing slash and a fragment removed. The path
/// and the query keep their case and order, because a site may tell them apart.
/// </summary>
public static class CanonicalUrl
{
    /// <summary>The canonical form of an absolute URL; any other text, such as a news query, is kept as it is.</summary>
    public static string Normalize(string address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Host.Length == 0)
        {
            return address;
        }

        var port = uri.IsDefaultPort ? string.Empty : ":" + uri.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var path = uri.AbsolutePath.TrimEnd('/');
        return $"{uri.Scheme.ToLowerInvariant()}://{uri.Host.ToLowerInvariant()}{port}{path}{uri.Query}";
    }
}
