namespace Keryx.Infrastructure.Sources;

/// <summary>How Keryx identifies itself to sources (L2-021 AC3). Documented in tools/deploy/configuration.md.</summary>
public sealed class FetchOptions
{
    public const string Section = "Keryx:Fetch";

    public const string MissingContactUrl = "Missing required setting: Keryx__Fetch__UserAgentContactUrl";

    /// <summary>An https page that says what the collector is and how to reach its operator.</summary>
    public Uri? UserAgentContactUrl { get; set; }
}
