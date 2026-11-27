using Keryx.Api.Consumer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Keryx.Api.Tests.Consumer;

/// <summary>An error returned as a result, such as [ApiController]'s automatic 400, is never cached (L2-032).</summary>
public sealed class EtagFilterTests
{
    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(201)]
    public async Task Given_a_GET_whose_result_is_not_200_When_the_filter_runs_Then_it_adds_no_ETag_and_no_lifetime(int status)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = HttpMethods.Get;
        var context = new ResultExecutingContext(
            new ActionContext(http, new RouteData(), new ActionDescriptor()),
            [],
            new ObjectResult(new ProblemDetails { Status = status }) { StatusCode = status },
            controller: new object());

        await new EtagFilter(Options.Create(new JsonOptions())).OnResultExecutionAsync(
            context, () => Task.FromResult(new ResultExecutedContext(context, [], context.Result, context.Controller)));

        Assert.False(http.Response.Headers.ContainsKey("ETag"));
        Assert.False(http.Response.Headers.ContainsKey("Cache-Control"));
    }
}
