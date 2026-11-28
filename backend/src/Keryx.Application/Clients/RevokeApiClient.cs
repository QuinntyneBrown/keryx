using Keryx.Application.Abstractions;
using Keryx.Application.Audit;
using Keryx.Application.Errors;
using Keryx.Domain.Audit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Clients;

/// <summary>Revokes a client's key (L2-036). A second revocation returns the client unchanged.</summary>
public sealed record RevokeApiClient(string Id) : IRequest<ApiClientDto>;

internal sealed class RevokeApiClientHandler(IAppDbContext db, IAuditTrail audit, TimeProvider time) : IRequestHandler<RevokeApiClient, ApiClientDto>
{
    public async Task<ApiClientDto> Handle(RevokeApiClient request, CancellationToken cancellationToken)
    {
        var client = await db.ApiClients.SingleOrDefaultAsync(client => client.PublicId == request.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new ApiClientNotFoundException();
        if (client.RevokedAt is null)
        {
            client.Revoke(time.GetUtcNow());
            audit.Record(AuditAction.ApiKeyRevoked, client.PublicId);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (await ClientUsage.ReadAsync(db, [client], time.GetUtcNow(), cancellationToken).ConfigureAwait(false))[0];
    }
}

/// <summary>No client has this id.</summary>
public sealed class ApiClientNotFoundException() : KeryxException(404, "client_not_found", "No API client has this id.");
