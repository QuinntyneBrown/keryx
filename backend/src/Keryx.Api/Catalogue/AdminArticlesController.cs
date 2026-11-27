using Keryx.Application.Catalogue;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Keryx.Api.Catalogue;

/// <summary>Articles, for staff (L2-011). Each action sends one request and checks nothing.</summary>
[ApiController]
[Route("admin/v1/articles")]
[Authorize(Policy = "Staff")]
public sealed class AdminArticlesController(ISender sender) : ControllerBase
{
    [HttpGet("{id}")]
    public Task<AdminArticleDto> Get(string id, CancellationToken cancellationToken) => sender.Send(new GetAdminArticle(id), cancellationToken);
}
