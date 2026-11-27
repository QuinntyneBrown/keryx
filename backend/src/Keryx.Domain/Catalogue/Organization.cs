using System.Text.RegularExpressions;

namespace Keryx.Domain.Catalogue;

/// <summary>What an organization is (L2-017).</summary>
public enum OrganizationKind
{
    Church,
    Ministry,
    Gym,
    Club,
    Network,
    Business,
    Other,
}

/// <summary>
/// The kind an organization gets from the name it publishes under: the first of church, ministry,
/// gym, club, network and business whose words the name holds, and <c>other</c> when none does.
/// </summary>
public static partial class OrganizationKinds
{
    private static readonly (OrganizationKind Kind, Regex Words)[] Rules =
    [
        (OrganizationKind.Church, Church()),
        (OrganizationKind.Ministry, Ministry()),
        (OrganizationKind.Gym, Gym()),
        (OrganizationKind.Club, Club()),
        (OrganizationKind.Network, Network()),
        (OrganizationKind.Business, Business()),
    ];

    public static OrganizationKind From(string name) =>
        Rules.FirstOrDefault(rule => rule.Words.IsMatch(name)) is { Words: not null } rule ? rule.Kind : OrganizationKind.Other;

    public static string Word(OrganizationKind kind) => kind.ToString().ToLowerInvariant();

    public static OrganizationKind Of(string word) => Enum.Parse<OrganizationKind>(word, ignoreCase: true);

    [GeneratedRegex(@"\b(?:church|churches|parish|cathedral|chapel|congregation)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Church();

    [GeneratedRegex(@"\b(?:ministry|ministries|mission|missions)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Ministry();

    [GeneratedRegex(@"\b(?:gym|gyms|fitness|studio|studios|crossfit|athletic centre|athletic center)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Gym();

    [GeneratedRegex(@"\b(?:club|clubs|league)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Club();

    [GeneratedRegex(@"\b(?:network|alliance|association|federation)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Network();

    [GeneratedRegex(@"\b(?:inc|ltd|llc|corp|corporation|company|co)\b\.?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Business();
}

/// <summary>
/// A church, ministry, gym, club, network or business that publishes Christian fitness news
/// (L2-017). It is one record however its sources spell its name (L2-010 AC5), keeps its public
/// website as its only contact detail (L2-081), and where it was seen (L2-011).
/// </summary>
public sealed class Organization
{
    private readonly List<Provenance> _provenance = [];

    private Organization()
    {
    }

    public long Id { get; private set; }

    /// <summary>The id the APIs show (<c>org_…</c>), drawn once and never changed.</summary>
    public string PublicId { get; private set; } = string.Empty;

    /// <summary>The name as the organization publishes it, from the first source that named it.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>The match key: case, punctuation, whitespace and a leading "The" ignored.</summary>
    public string NormalizedName { get; private set; } = string.Empty;

    public OrganizationKind Kind { get; private set; }

    public string RegionSlug { get; private set; } = string.Empty;

    /// <summary>The organization's public website, the scheme and host of its first web source; null when it has none.</summary>
    public string? WebsiteUrl { get; private set; }

    public IReadOnlyList<Provenance> Provenance => _provenance;

    public static Organization Named(string name, string regionSlug, string? sourceAddress, long sourceId, string sourceName, DateTimeOffset seenAt)
    {
        var organization = new Organization
        {
            PublicId = PublicIds.New("org"),
            Name = name.Trim(),
            NormalizedName = OrganizationName.Normalize(name),
            Kind = OrganizationKinds.From(name),
            RegionSlug = regionSlug,
            WebsiteUrl = WebsiteOf(sourceAddress),
        };
        organization._provenance.Add(new Provenance(sourceId, sourceName, sourceAddress ?? string.Empty, seenAt));
        return organization;
    }

    /// <summary>A source naming it was seen: its entry moves, or the source gets one.</summary>
    public void Seen(long sourceId, string sourceName, string sourceAddress, DateTimeOffset seenAt)
    {
        var entry = _provenance.FirstOrDefault(candidate => candidate.SourceId == sourceId);
        if (entry is null)
        {
            _provenance.Add(new Provenance(sourceId, sourceName, sourceAddress, seenAt));
            WebsiteUrl ??= WebsiteOf(sourceAddress);
        }
        else
        {
            entry.SeenAgain(seenAt);
        }
    }

    private static string? WebsiteOf(string? address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? $"{uri.Scheme}://{uri.Host.ToLowerInvariant()}"
            : null;
}
