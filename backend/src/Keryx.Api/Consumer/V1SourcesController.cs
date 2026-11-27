using Keryx.Application.Consumer;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Keryx.Api.Consumer;

/// <summary>The sources Keryx collects from (L2-029 AC4), under the <c>sources:read</c> scope. It sends one request.</summary>
[ApiController]
[Route("v1/sources")]
[ConditionalGet]
[Authorize(Policy = ApiKeyDefaults.SourcesRead)]
public sealed class V1SourcesController(ISender sender) : ControllerBase
{
    /// <summary>A page of active sources by name: name, kind, organization and region only.</summary>
    [HttpGet]
    [ProducesErrorCode(StatusCodes.Status400BadRequest, "invalid_limit")]
    [ProducesErrorCode(StatusCodes.Status400BadRequest, "invalid_cursor")]
    public Task<PageDto<PublicSourceDto>> List([FromQuery] string? limit, [FromQuery] string? cursor, CancellationToken cancellationToken) =>
        sender.Send(new ListPublicSources(limit, cursor), cancellationToken);
}
