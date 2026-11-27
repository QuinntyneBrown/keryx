using Keryx.Application.Consumer;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Keryx.Api.Consumer;

/// <summary>Published articles for consuming products (L2-026, L2-027), under the <c>articles:read</c> scope. Each action sends one request.</summary>
[ApiController]
[Route("v1/articles")]
[Authorize(Policy = ApiKeyDefaults.ArticlesRead)]
public sealed class ArticlesController(ISender sender) : ControllerBase
{
    [HttpGet("{id}")]
    public Task<ArticleDetailDto> Get(string id, CancellationToken cancellationToken) =>
        sender.Send(new GetArticle(id), cancellationToken);
}
