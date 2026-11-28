using Keryx.Application.Abstractions;
using Keryx.Domain.Sources;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Sources;

/// <summary>A source as the admin API serves it; kind and health are the API's words.</summary>
public sealed record SourceDto(
    string Id,
    string Kind,
    string Address,
    string OrganizationName,
    string Region,
    string Health,
    string? FailureReason,
    int ConsecutiveFailures,
    int ArticleCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastFetchedAt,
    DateTimeOffset? LastSucceededAt)
{
    internal static SourceDto From(Source source, int articleCount) => new(
        source.PublicId,
        SourceWords.Kind(source.Kind),
        source.Address,
        source.OrganizationName,
        source.RegionSlug,
        SourceWords.Health(source.Health),
        source.FailureReason,
        source.ConsecutiveFailures,
        articleCount,
        source.CreatedAt,
        source.LastFetchedAt,
        source.LastSucceededAt);

    /// <summary>The source with the number of articles collected from it, which the remove dialog says (L2-065 AC3).</summary>
    internal static async Task<SourceDto> ReadAsync(IAppDbContext db, Source source, CancellationToken cancellationToken) =>
        From(source, await db.Articles.CountAsync(
            article => article.Provenance.Any(entry => entry.SourceId == source.Id), cancellationToken).ConfigureAwait(false));
}
