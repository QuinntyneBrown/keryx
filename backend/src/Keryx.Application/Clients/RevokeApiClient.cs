using Keryx.Application.Abstractions;
using Keryx.Application.Errors;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Clients;

/// <summary>Revokes a client's key (L2-036). A second revocation returns the client unchanged.</summary>
public sealed record RevokeApiClient(string Id) : IRequest<ApiClientDto>;

internal sealed class RevokeApiClientHandler(IAppDbContext db, TimeProvider time) : IRequestHandler<RevokeApiClient, ApiClientDto>
{
    public async Task<ApiClientDto> Handle(RevokeApiClient request, CancellationToken cancellationToken)
    {
        var client = await db.ApiClients.SingleOrDefaultAsync(client => client.PublicId == request.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new ApiClientNotFoundException();
        client.Revoke(time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ApiClientDto.From(client);
    }
}

/// <summary>No client has this id.</summary>
public sealed class ApiClientNotFoundException() : KeryxException(404, "client_not_found", "No API client has this id.");
