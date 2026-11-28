using Keryx.Application.Clients;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Keryx.Api.Clients;

/// <summary>The consumer API's usage, for staff (L2-037 AC3). It sends one request and checks nothing.</summary>
[ApiController]
[Route("admin/v1/api-usage")]
[Authorize(Policy = "Staff")]
public sealed class AdminApiUsageController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<ApiUsageDto> Get(CancellationToken cancellationToken) => sender.Send(new GetApiUsage(), cancellationToken);
}
