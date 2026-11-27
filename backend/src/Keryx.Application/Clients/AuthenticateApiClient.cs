using Keryx.Application.Abstractions;
using Keryx.Domain.Clients;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Clients;

/// <summary>The key a request presented in its <c>Authorization</c> header (L2-033).</summary>
public sealed record AuthenticateApiClient(string Key) : IRequest<ApiClientPrincipal?>;

/// <summary>The client a key belongs to: its public id and its scopes.</summary>
public sealed record ApiClientPrincipal(string Id, IReadOnlyList<string> Scopes);

/// <summary>
/// Finds the active client whose stored hash the key matches. It reads the database on every
/// request and caches nothing, so a revocation takes effect on the next request (L2-036 AC1).
/// </summary>
internal sealed class AuthenticateApiClientHandler(IAppDbContext db) : IRequestHandler<AuthenticateApiClient, ApiClientPrincipal?>
{
    public async Task<ApiClientPrincipal?> Handle(AuthenticateApiClient request, CancellationToken cancellationToken)
    {
        if (!ApiKeyFormat.IsWellFormed(request.Key))
        {
            return null;
        }

        var prefix = request.Key[..ApiClient.PrefixLength];
        var candidates = await db.ApiClients.AsNoTracking()
            .Where(client => client.KeyPrefix == prefix && client.RevokedAt == null)
            .Select(client => new { client.PublicId, client.KeyHash, client.Scopes })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var match = candidates.FirstOrDefault(client => ApiKeyHash.Matches(request.Key, client.KeyHash));
        return match is null ? null : new ApiClientPrincipal(match.PublicId, match.Scopes);
    }
}
