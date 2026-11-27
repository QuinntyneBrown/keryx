namespace Keryx.Domain.Reference;

/// <summary>
/// A place a record belongs to (L2-018). Toronto comes first: regions are ranked, the GTA
/// municipalities before the wider areas, and each carries the IANA zone its times are shown in.
/// </summary>
public sealed class Region
{
    public Region(string slug, string name, string timeZone, int rank)
    {
        Slug = slug;
        Name = name;
        TimeZone = timeZone;
        Rank = rank;
    }

    /// <summary>The stable key: lowercase words joined by hyphens (<c>richmond-hill</c>).</summary>
    public string Slug { get; private set; }

    public string Name { get; private set; }

    /// <summary>An IANA zone (<c>America/Toronto</c>).</summary>
    public string TimeZone { get; private set; }

    /// <summary>The place in the list: a lower rank comes first.</summary>
    public int Rank { get; private set; }
}
