using Keryx.Domain.Sources;

namespace Keryx.Application.Sources;

/// <summary>
/// Fetches a source over the network (L2-021 to L2-024). Every fetch goes through this contract,
/// so no handler or stage opens a connection of its own; Infrastructure implements it.
/// </summary>
public interface ISourceFetcher
{
    Task<SourceFetchResult> FetchAsync(SourceFetchRequest request, CancellationToken cancellationToken);
}

/// <summary>One fetch: what the source is, where, the validators of the last fetch, and how long it may take.</summary>
public sealed record SourceFetchRequest(SourceKind Kind, Uri Address, TimeSpan Timeout, string? ETag = null, DateTimeOffset? LastModified = null);

/// <summary>Why a fetch failed: the fixed list of L2-002 AC2.</summary>
public enum FetchFailureReason
{
    NotFound,
    Timeout,
    BlockedByRobots,
    Refused,
    InvalidContent,
    UnsafeAddress,
}

/// <summary>What a fetch returned: bounded content and its type, nothing new, or why it failed.</summary>
public sealed record SourceFetchResult(bool Succeeded, bool NotModified, string Content, string? ContentType, FetchFailureReason? Failure)
{
    /// <summary>
    /// The source asked not to be indexed (<c>X-Robots-Tag: noindex</c>): nothing from it is stored,
    /// and its body was not read (L2-080 AC4).
    /// </summary>
    public bool NoIndex { get; init; }

    public static SourceFetchResult Fetched(string content, string? contentType) => new(true, false, content, contentType, null);

    public static SourceFetchResult Unchanged() => new(true, true, string.Empty, null, null);

    public static SourceFetchResult Failed(FetchFailureReason reason) => new(false, false, string.Empty, null, reason);
}
