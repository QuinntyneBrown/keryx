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
}

/// <summary>The fields a create reads; any other in the body is ignored (L2-083).</summary>
public sealed record CreateApiClientBody(string? ProductName, IReadOnlyList<string>? Scopes);
