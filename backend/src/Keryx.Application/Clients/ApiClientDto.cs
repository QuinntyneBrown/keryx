using Keryx.Domain.Clients;

namespace Keryx.Application.Clients;

/// <summary>A client as staff read it: never its key, only the key's first 12 characters (L2-035 AC2).</summary>
public sealed record ApiClientDto(string Id, string ProductName, string KeyPrefix, IReadOnlyList<string> Scopes, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt)
{
    public static ApiClientDto From(ApiClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        return new ApiClientDto(client.PublicId, client.ProductName, client.KeyPrefix, client.Scopes, client.CreatedAt, client.RevokedAt);
    }
}

/// <summary>A new client and its full key, which this response alone carries.</summary>
public sealed record CreatedApiClientDto(ApiClientDto Client, string Key);

/// <summary>A page of clients, newest first; every count is a number.</summary>
public sealed record ApiClientPage(IReadOnlyList<ApiClientDto> Items, int Total, int Page, int PageSize);
