using Keryx.Application.Runs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Keryx.Api.Runs;

/// <summary>Worker runs, for staff (L2-019). Each action sends one request and checks nothing.</summary>
[ApiController]
[Route("admin/v1/runs")]
[Authorize(Policy = "Staff")]
public sealed class AdminRunsController(ISender sender) : ControllerBase
{
    [HttpGet("{id}")]
    public Task<RunDto> Get(string id, CancellationToken cancellationToken) => sender.Send(new GetRun(id), cancellationToken);
}
