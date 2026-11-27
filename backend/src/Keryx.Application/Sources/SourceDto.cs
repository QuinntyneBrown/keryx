using Keryx.Domain.Sources;

namespace Keryx.Application.Sources;

/// <summary>A source as the admin API serves it; kind and health are the API's words.</summary>
public sealed record SourceDto(
    string Id,
    string Kind,
    string Address,
    string OrganizationName,
    string Region,
    string Health,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastFetchedAt,
    DateTimeOffset? LastSucceededAt)
{
    internal static SourceDto From(Source source) => new(
        source.PublicId,
        SourceWords.Kind(source.Kind),
        source.Address,
        source.OrganizationName,
        source.RegionSlug,
        SourceWords.Health(source.Health),
        source.CreatedAt,
        source.LastFetchedAt,
        source.LastSucceededAt);
}
