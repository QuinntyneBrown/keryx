using System.Security.Claims;
using Keryx.Application.Session;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Keryx.Api.Session;

[ApiController]
[Route("admin/v1/me")]
[Authorize(Policy = "Staff")]
public sealed class CurrentStaffController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<StaffProfile> Get(CancellationToken cancellationToken) =>
        sender.Send(new GetCurrentStaff(User.FindFirstValue("sub")), cancellationToken);
}
