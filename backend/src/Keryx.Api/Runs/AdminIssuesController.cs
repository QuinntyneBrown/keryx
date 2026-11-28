using Keryx.Application.Runs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Keryx.Api.Runs;

/// <summary>Issues runs found, for staff (L2-045). Each action sends one request and checks nothing.</summary>
[ApiController]
[Route("admin/v1/issues")]
[Authorize(Policy = "Staff")]
public sealed class AdminIssuesController(ISender sender) : ControllerBase
{
    /// <summary>The open issues, most severe first, with how many are open in all.</summary>
    [HttpGet]
    public Task<OpenIssuePageDto> List(
        [FromQuery] string? state,
        [FromQuery(Name = "page_size")] string? pageSize,
        CancellationToken cancellationToken) =>
        sender.Send(new ListOpenIssues(state, pageSize), cancellationToken);

    /// <summary>Mutes an open issue: it stops counting as open and raising alerts (L2-020 AC2).</summary>
    [HttpPost("{id}/mute")]
    public Task<RunIssueDto> Mute(string id, CancellationToken cancellationToken) =>
        sender.Send(new MuteIssueCommand(id), cancellationToken);

    /// <summary>Unmutes a muted issue: it is open again (L2-020 AC3).</summary>
    [HttpPost("{id}/unmute")]
    public Task<RunIssueDto> Unmute(string id, CancellationToken cancellationToken) =>
        sender.Send(new UnmuteIssueCommand(id), cancellationToken);
}
