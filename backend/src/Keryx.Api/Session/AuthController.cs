using Keryx.Application.Session;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Keryx.Api.Session;

[ApiController]
[Route("admin/v1/auth")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("sign-out")]
    public async Task<IActionResult> End()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpGet("sign-in")]
    public async Task<IActionResult> Start([FromQuery(Name = "return")] string? returnPath, CancellationToken cancellationToken)
    {
        var path = await sender.Send(new PrepareSignIn(returnPath), cancellationToken).ConfigureAwait(false);
        return Challenge(new AuthenticationProperties { RedirectUri = path }, OpenIdConnectDefaults.AuthenticationScheme);
    }
}
