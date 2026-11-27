namespace Keryx.Domain.Clients;

/// <summary>
/// A product that may call the consumer API (L2-035, L2-036): its name, scopes and key. Keryx
/// keeps only the key's SHA-256 hash and its first 12 characters, so the key cannot be shown
/// again; a revoked client stays listed and is never made active again.
/// </summary>
public sealed class ApiClient
{
    /// <summary>How many of a key's first characters staff see again.</summary>
    public const int PrefixLength = 12;

    public const int MaxNameLength = 80;

    private ApiClient()
    {
    }

    public long Id { get; private set; }

    /// <summary>The id the admin API shows (<c>cli_…</c>), drawn once and never changed.</summary>
    public string PublicId { get; private set; } = string.Empty;

    public string ProductName { get; private set; } = string.Empty;

    /// <summary>The name's match key: two active clients cannot share it, whatever the case.</summary>
    public string NormalizedName { get; private set; } = string.Empty;

    public IReadOnlyList<string> Scopes { get; private set; } = [];

    public string KeyPrefix { get; private set; } = string.Empty;

    /// <summary>The key's SHA-256 hash, lower-case hex.</summary>
    public string KeyHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When it was revoked, in UTC; null while active. Set once and never cleared.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsActive => RevokedAt is null;

    public static ApiClient Create(string productName, IEnumerable<string> scopes, string key, string keyHash, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(productName);
        ArgumentNullException.ThrowIfNull(key);
        var name = productName.Trim();
        return new ApiClient
        {
            PublicId = PublicIds.New("cli"),
            ProductName = name,
            NormalizedName = Normalize(name),
            Scopes = [.. ApiScopes.All.Where(scopes.Contains)],
            KeyPrefix = key[..PrefixLength],
            KeyHash = keyHash,
            CreatedAt = now,
        };
    }

    public static string Normalize(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.Trim().ToUpperInvariant();
    }

    /// <summary>Ends the key's validity; a second revocation changes nothing (L2-036 AC2).</summary>
    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}

/// <summary>What a key may read (L2-035).</summary>
public static class ApiScopes
{
    public const string ArticlesRead = "articles:read";
    public const string EventsRead = "events:read";
    public const string SourcesRead = "sources:read";

    /// <summary>Every scope, in the order the dialog lists them.</summary>
    public static readonly IReadOnlyList<string> All = [ArticlesRead, EventsRead, SourcesRead];
}
