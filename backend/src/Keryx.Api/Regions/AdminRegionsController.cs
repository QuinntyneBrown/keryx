using Keryx.Application.Regions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Keryx.Api.Regions;

/// <summary>The region list for staff screens, such as the add-source dialog (L2-001, L2-018).</summary>
[ApiController]
[Route("admin/v1/regions")]
[Authorize(Policy = "Staff")]
public sealed class AdminRegionsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<RegionList> List(CancellationToken cancellationToken) => sender.Send(new ListRegions(), cancellationToken);
}
