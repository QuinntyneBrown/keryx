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
    /// <summary>A page of a worker's runs, newest first; <c>problems=true</c> keeps those that failed or completed with issues.</summary>
    [HttpGet]
    public Task<RunPageDto> List(
        [FromQuery] string? worker,
        [FromQuery] string? problems,
        [FromQuery] string? page,
        [FromQuery(Name = "page_size")] string? pageSize,
        CancellationToken cancellationToken) =>
        sender.Send(new ListRuns(worker, problems, page, pageSize), cancellationToken);

    /// <summary>Starts a collection run now: <c>202</c>, or <c>409</c> <c>run_in_progress</c> with the running run's id.</summary>
    [HttpPost]
    [ProducesResponseType<RunStartedDto>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Start(CancellationToken cancellationToken) =>
        Accepted(await sender.Send(new StartRun(), cancellationToken).ConfigureAwait(false));

    [HttpGet("{id}")]
    public Task<RunDto> Get(string id, CancellationToken cancellationToken) => sender.Send(new GetRun(id), cancellationToken);
}
