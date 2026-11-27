namespace Keryx.Application.Sources;

/// <summary>
/// What a source's address must be before Keryx stores or fetches it (L2-001 AC2, L2-024): an
/// absolute <c>http</c> or <c>https</c> URL with a host, no user name or password, and no port
/// other than 80 or 443. Whether its host is public is checked when it is resolved.
/// </summary>
public static class SourceAddress
{
    public const int MaxLength = 2048;

    public static bool IsAcceptable(string? address) =>
        address is { Length: > 0 and <= MaxLength }
        && Uri.TryCreate(address, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && uri.Host.Length > 0
        && uri.UserInfo.Length == 0
        && (uri.IsDefaultPort || uri.Port is 80 or 443);
}
