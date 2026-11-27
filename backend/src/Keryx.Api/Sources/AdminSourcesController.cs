using Keryx.Application.Sources;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Keryx.Api.Sources;

/// <summary>The source catalogue, for staff (L2-001). Each action sends one request and checks nothing.</summary>
[ApiController]
[Route("admin/v1/sources")]
[Authorize(Policy = "Staff")]
public sealed class AdminSourcesController(ISender sender) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<SourceDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateSourceBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var source = await sender.Send(
            new CreateSource(body.Kind, body.Address, body.OrganizationName, body.Region), cancellationToken).ConfigureAwait(false);
        return Created($"/admin/v1/sources/{source.Id}", source);
    }

    [HttpGet("{id}")]
    public Task<SourceDto> Get(string id, CancellationToken cancellationToken) =>
        sender.Send(new GetSource(id), cancellationToken);
}

/// <summary>What staff send to add a source.</summary>
public sealed record CreateSourceBody(string? Kind, string? Address, string? OrganizationName, string? Region);
