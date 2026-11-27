using Keryx.Application.Consumer;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Keryx.Api.Consumer;

/// <summary>Upcoming served events for consuming products (L2-028), under the <c>events:read</c> scope. Each action sends one request.</summary>
[ApiController]
[Route("v1/events")]
[ConditionalGet]
[Authorize(Policy = ApiKeyDefaults.EventsRead)]
public sealed class EventsController(ISender sender) : ControllerBase
{
    /// <summary>A page of upcoming events, soonest first; follow <c>next_cursor</c> for the next.</summary>
    [HttpGet]
    [ProducesErrorCode(StatusCodes.Status400BadRequest, "invalid_limit")]
    [ProducesErrorCode(StatusCodes.Status400BadRequest, "invalid_filter")]
    [ProducesErrorCode(StatusCodes.Status400BadRequest, "invalid_cursor")]
    public Task<PageDto<EventDto>> List(
        [FromQuery] string? limit,
        [FromQuery] string? cursor,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? region,
        [FromQuery] string? org,
        CancellationToken cancellationToken) =>
        sender.Send(new ListEvents(limit, cursor, from, to, region, org), cancellationToken);

    /// <summary>One served event, upcoming or past.</summary>
    [HttpGet("{id}")]
    [ProducesErrorCode(StatusCodes.Status404NotFound, "event_not_found")]
    public Task<EventDto> Get(string id, CancellationToken cancellationToken) =>
        sender.Send(new GetEvent(id), cancellationToken);
}
