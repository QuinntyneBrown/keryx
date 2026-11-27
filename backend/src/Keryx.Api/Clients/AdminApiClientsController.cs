using Keryx.Application.Clients;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Keryx.Api.Clients;

/// <summary>The products that may call the consumer API, for staff (L2-035, L2-036). Each action sends one request and checks nothing.</summary>
[ApiController]
[Route("admin/v1/api-clients")]
[Authorize(Policy = "Staff")]
public sealed class AdminApiClientsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<ApiClientPage> List([FromQuery] int page = 1, [FromQuery(Name = "page_size")] int pageSize = 50, CancellationToken cancellationToken = default) =>
        sender.Send(new ListApiClients(page, pageSize), cancellationToken);

    /// <summary>Creates a key; the response alone carries it in full.</summary>
    [HttpPost]
    [ProducesResponseType<CreatedApiClientDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateApiClientBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var created = await sender.Send(new CreateApiClient(body.ProductName, body.Scopes), cancellationToken).ConfigureAwait(false);
        return Created($"/admin/v1/api-clients/{created.Client.Id}", created);
    }

    /// <summary>Revokes a key; the next request with it gets 401, and nothing makes it active again.</summary>
    [HttpPost("{id}/revoke")]
    public async Task<RevokedApiClientDto> Revoke(string id, CancellationToken cancellationToken) =>
        new(await sender.Send(new RevokeApiClient(id), cancellationToken).ConfigureAwait(false));
}

/// <summary>The revoked client, with its revocation time.</summary>
public sealed record RevokedApiClientDto(ApiClientDto Client);

/// <summary>The fields a create reads; any other in the body is ignored (L2-083).</summary>
public sealed record CreateApiClientBody(string? ProductName, IReadOnlyList<string>? Scopes);
