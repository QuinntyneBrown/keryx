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
    /// <summary>A page of every collected article, newest collected first, narrowed by status, search, region and category.</summary>
    [HttpGet]
    public Task<AdminArticlePageDto> List(
        [FromQuery] string? page,
        [FromQuery(Name = "page_size")] string? pageSize,
        [FromQuery] string? status,
        [FromQuery] string? q,
        [FromQuery] string? region,
        [FromQuery] string? category,
        CancellationToken cancellationToken) =>
        sender.Send(new ListAdminArticles(page, pageSize, status, q, region, category), cancellationToken);

    /// <summary>The whole total of each status, for the tabs.</summary>
    [HttpGet("counts")]
    public Task<ArticleStatusCountsDto> Counts(CancellationToken cancellationToken) => sender.Send(new GetArticleStatusCounts(), cancellationToken);

    [HttpGet("{id}")]
    public Task<AdminArticleDto> Get(string id, CancellationToken cancellationToken) => sender.Send(new GetAdminArticle(id), cancellationToken);

    /// <summary>Publishes the article whatever its score; the same decision again changes nothing.</summary>
    [HttpPost("{id}/publish")]
    public Task<ArticleDecisionDto> Publish(string id, CancellationToken cancellationToken) => sender.Send(new PublishArticle(id), cancellationToken);
}
