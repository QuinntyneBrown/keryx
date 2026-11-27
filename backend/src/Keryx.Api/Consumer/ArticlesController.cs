using Keryx.Application.Consumer;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Keryx.Api.Consumer;

/// <summary>Published articles for consuming products (L2-026, L2-027), under the <c>articles:read</c> scope. Each action sends one request.</summary>
[ApiController]
[Route("v1/articles")]
[ConditionalGet]
[Authorize(Policy = ApiKeyDefaults.ArticlesRead)]
public sealed class ArticlesController(ISender sender) : ControllerBase
{
    /// <summary>A page of published articles, newest first; follow <c>next_cursor</c> for the next.</summary>
    [HttpGet]
    [ProducesErrorCode(StatusCodes.Status400BadRequest, "invalid_limit")]
    [ProducesErrorCode(StatusCodes.Status400BadRequest, "invalid_filter")]
    [ProducesErrorCode(StatusCodes.Status400BadRequest, "invalid_cursor")]
    public Task<PageDto<ArticleSummaryDto>> List(
        [FromQuery] string? limit,
        [FromQuery] string? cursor,
        [FromQuery] string? region,
        [FromQuery] string? category,
        [FromQuery] string? org,
        [FromQuery] string? since,
        CancellationToken cancellationToken) =>
        sender.Send(new ListArticles(limit, cursor, region, category, org, since), cancellationToken);

    /// <summary>One published article with its tags and every source that reported it.</summary>
    [HttpGet("{id}")]
    [ProducesErrorCode(StatusCodes.Status404NotFound, "article_not_found")]
    public Task<ArticleDetailDto> Get(string id, CancellationToken cancellationToken) =>
        sender.Send(new GetArticle(id), cancellationToken);
}
