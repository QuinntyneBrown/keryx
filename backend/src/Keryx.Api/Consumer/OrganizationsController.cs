using Keryx.Application.Consumer;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Keryx.Api.Consumer;

/// <summary>Organizations with something published (L2-029), under the <c>sources:read</c> scope. Each action sends one request.</summary>
[ApiController]
[Route("v1/organizations")]
[ConditionalGet]
[Authorize(Policy = ApiKeyDefaults.SourcesRead)]
public sealed class OrganizationsController(ISender sender) : ControllerBase
{
    /// <summary>A page of listed organizations by name; follow <c>next_cursor</c> for the next.</summary>
    [HttpGet]
    [ProducesErrorCode(StatusCodes.Status400BadRequest, "invalid_limit")]
    [ProducesErrorCode(StatusCodes.Status400BadRequest, "invalid_filter")]
    [ProducesErrorCode(StatusCodes.Status400BadRequest, "invalid_cursor")]
    public Task<PageDto<OrganizationDto>> List(
        [FromQuery] string? limit,
        [FromQuery] string? cursor,
        [FromQuery] string? region,
        [FromQuery] string? kind,
        [FromQuery] string? q,
        CancellationToken cancellationToken) =>
        sender.Send(new ListOrganizations(limit, cursor, region, kind, q), cancellationToken);

    /// <summary>One listed organization.</summary>
    [HttpGet("{id}")]
    [ProducesErrorCode(StatusCodes.Status404NotFound, "organization_not_found")]
    public Task<OrganizationDto> Get(string id, CancellationToken cancellationToken) =>
        sender.Send(new GetOrganization(id), cancellationToken);
}
