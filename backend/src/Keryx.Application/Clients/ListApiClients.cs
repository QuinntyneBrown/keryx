using Keryx.Application.Abstractions;
using Keryx.Application.Errors;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Clients;

/// <summary>A page of clients, active and revoked alike, newest first.</summary>
public sealed record ListApiClients(int Page, int PageSize) : IRequest<ApiClientPage>;

internal sealed class ListApiClientsHandler(IAppDbContext db) : IRequestHandler<ListApiClients, ApiClientPage>
{
    /// <summary>The API screen's page size, and the largest a request may ask for.</summary>
    public const int MaxPageSize = 50;

    public async Task<ApiClientPage> Handle(ListApiClients request, CancellationToken cancellationToken)
    {
        if (request.Page < 1 || request.PageSize is < 1 or > MaxPageSize)
        {
            throw new InvalidPageException();
        }

        var total = await db.ApiClients.CountAsync(cancellationToken).ConfigureAwait(false);
        var clients = await db.ApiClients.AsNoTracking()
            .OrderByDescending(client => client.CreatedAt).ThenByDescending(client => client.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return new ApiClientPage([.. clients.Select(ApiClientDto.From)], total, request.Page, request.PageSize);
    }
}
