namespace Keryx.Domain.Sources;

/// <summary>
/// Somewhere Keryx collects from (L2-001): a kind, an address (a URL, or the query text of a
/// news query), the organization it belongs to and its region. A seeded source also carries the
/// fixed key that makes seeding idempotent.
/// </summary>
public sealed class Source
{
    private Source()
    {
    }

    public long Id { get; private set; }

    /// <summary>The id the API shows (<c>src_…</c>), drawn once and never changed.</summary>
    public string PublicId { get; private set; } = string.Empty;

    public SourceKind Kind { get; private set; }

    /// <summary>A URL, or for a news query the query text.</summary>
    public string Address { get; private set; } = string.Empty;

    public string OrganizationName { get; private set; } = string.Empty;

    public string RegionSlug { get; private set; } = string.Empty;

    /// <summary>When it was created, in UTC.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The fixed key of a seeded source; a source staff added has none.</summary>
    public string? SeedKey { get; private set; }

    /// <summary>A new source is healthy until a fetch says otherwise (L2-004 AC5).</summary>
    public SourceHealth Health { get; private set; } = SourceHealth.Healthy;

    /// <summary>When it was last fetched, in UTC; null until its first fetch.</summary>
    public DateTimeOffset? LastFetchedAt { get; private set; }

    /// <summary>When a fetch of it last succeeded, in UTC.</summary>
    public DateTimeOffset? LastSucceededAt { get; private set; }

    public int ConsecutiveFailures { get; private set; }

    /// <summary>A source staff added (L2-001); the next collection run fetches it.</summary>
    public static Source Add(SourceKind kind, string address, string organizationName, string regionSlug, DateTimeOffset now) =>
        new()
        {
            PublicId = PublicIds.New("src"),
            Kind = kind,
            Address = address,
            OrganizationName = organizationName,
            RegionSlug = regionSlug,
            CreatedAt = now,
        };

    /// <summary>A news query source that seed data creates, once, under <paramref name="seedKey"/>.</summary>
    public static Source SeededNewsQuery(string seedKey, string organizationName, string query, string regionSlug, DateTimeOffset now) =>
        new()
        {
            PublicId = PublicIds.New("src"),
            Kind = SourceKind.NewsQuery,
            Address = query,
            OrganizationName = organizationName,
            RegionSlug = regionSlug,
            CreatedAt = now,
            SeedKey = seedKey,
        };
}
