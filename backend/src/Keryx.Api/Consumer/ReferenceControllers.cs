using Keryx.Application.Consumer;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Keryx.Api.Consumer;

/// <summary>The categories (L2-030 AC1); any valid key reads them. It sends one request.</summary>
[ApiController]
[Route("v1/categories")]
[ConditionalGet]
[Authorize(Policy = ApiKeyDefaults.AnyClient)]
public sealed class CategoriesController(ISender sender) : ControllerBase
{
    /// <summary>The six categories in order, each with its count of published articles.</summary>
    [HttpGet]
    public Task<ListDto<CategoryDto>> List(CancellationToken cancellationToken) => sender.Send(new ListCategories(), cancellationToken);
}

/// <summary>The regions (L2-030 AC2); any valid key reads them. It sends one request.</summary>
[ApiController]
[Route("v1/regions")]
[ConditionalGet]
[Authorize(Policy = ApiKeyDefaults.AnyClient)]
public sealed class RegionsController(ISender sender) : ControllerBase
{
    /// <summary>The regions by rank, Toronto first and global last, each with its zone and count of published articles.</summary>
    [HttpGet]
    public Task<ListDto<RegionDto>> List(CancellationToken cancellationToken) => sender.Send(new ListRegions(), cancellationToken);
}

/// <summary>The freshness of collection (L2-030 AC3, AC4); any valid key reads it. It sends one request.</summary>
[ApiController]
[Route("v1/status")]
[ConditionalGet]
[Authorize(Policy = ApiKeyDefaults.AnyClient)]
public sealed class StatusController(ISender sender) : ControllerBase
{
    /// <summary>The latest collection run's start and outcome, the next scheduled run and the published total.</summary>
    [HttpGet]
    public Task<StatusDto> Get(CancellationToken cancellationToken) => sender.Send(new GetStatus(), cancellationToken);
}
