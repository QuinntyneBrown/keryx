using Keryx.Application.Sources;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Keryx.Api.Sources;

/// <summary>The source catalogue, for staff (L2-001). Each action sends one request and checks nothing.</summary>
[ApiController]
[Route("admin/v1/sources")]
[Authorize(Policy = "Staff")]
public sealed class AdminSourcesController(ISender sender) : ControllerBase
{
    /// <summary>One page of the catalogue, failing first (L2-062). The screen sends a page size of 12.</summary>
    [HttpGet]
    public Task<SourcePage> List(
        [FromQuery] string? kind,
        [FromQuery] int page = 1,
        [FromQuery(Name = "page_size")] int pageSize = 12,
        CancellationToken cancellationToken = default) =>
        sender.Send(new ListSources(kind, page, pageSize), cancellationToken);

    [HttpPost]
    [ProducesResponseType<SourceDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateSourceBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var source = await sender.Send(
            new CreateSource(body.Kind, body.Address, body.OrganizationName, body.Region), cancellationToken).ConfigureAwait(false);
        return Created($"/admin/v1/sources/{source.Id}", source);
    }

    /// <summary>Test-fetches an address before it is added; an unreachable one is still 200 (L2-002).</summary>
    [HttpPost("test")]
    public Task<SourceTestResult> Test(TestSourceBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        return sender.Send(new TestSourceFetch(body.Kind, body.Address), cancellationToken);
    }

    /// <summary>Test-fetches a source in the catalogue without changing its health (L2-002 AC3).</summary>
    [HttpPost("{id}/test")]
    public Task<SourceTestResult> TestExisting(string id, CancellationToken cancellationToken) =>
        sender.Send(new TestExistingSourceFetch(id), cancellationToken);

    [HttpGet("{id}")]
    public Task<SourceDto> Get(string id, CancellationToken cancellationToken) =>
        sender.Send(new GetSource(id), cancellationToken);

    /// <summary>Saves a source's address, organization and region, and test-fetches the saved address (L2-003 AC1).</summary>
    [HttpPut("{id}")]
    public Task<SourceSaved> Update(string id, UpdateSourceBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        return sender.Send(new UpdateSource(id, body.Address, body.OrganizationName, body.Region), cancellationToken);
    }
}

/// <summary>What staff send to change a source; its kind is not editable (L2-003).</summary>
public sealed record UpdateSourceBody(string? Address, string? OrganizationName, string? Region);

/// <summary>What staff send to add a source.</summary>
public sealed record CreateSourceBody(string? Kind, string? Address, string? OrganizationName, string? Region);

/// <summary>What staff send to test an address.</summary>
public sealed record TestSourceBody(string? Kind, string? Address);
