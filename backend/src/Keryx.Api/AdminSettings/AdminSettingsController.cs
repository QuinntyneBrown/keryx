using Keryx.Application.AdminSettings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Keryx.Api.AdminSettings;

/// <summary>Settings, for staff (L2-070, L2-071). Each action sends one request and checks nothing.</summary>
[ApiController]
[Route("admin/v1/settings")]
[Authorize(Policy = "Staff")]
public sealed class AdminSettingsController(ISender sender) : ControllerBase
{
    /// <summary>Every setting and the version it was read at.</summary>
    [HttpGet]
    public Task<SettingsDto> Get(CancellationToken cancellationToken) => sender.Send(new GetSettings(), cancellationToken);

    /// <summary>Saves the settings from the version they were loaded at: 400 <c>validation_failed</c>, 409 <c>settings_changed</c>.</summary>
    [HttpPut]
    public Task<SettingsDto> Update(UpdateSettings request, CancellationToken cancellationToken) => sender.Send(request, cancellationToken);
}
