using Keryx.Application.Runs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Keryx.Api.Runs;

/// <summary>Overview, for staff (L2-043). Each action sends one request and checks nothing.</summary>
[ApiController]
[Route("admin/v1/overview")]
[Authorize(Policy = "Staff")]
public sealed class AdminOverviewController(ISender sender) : ControllerBase
{
    /// <summary>The last and current collection runs, whether any source and run exist, and the next run.</summary>
    [HttpGet("status")]
    public Task<OverviewStatusDto> Status(CancellationToken cancellationToken) => sender.Send(new GetOverviewStatus(), cancellationToken);
}
