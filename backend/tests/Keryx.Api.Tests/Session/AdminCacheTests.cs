using System.Net;
using Keryx.Api.Tests.Probes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Keryx.Api.Tests.Session;

// L2-041 and L2-082. Shared and browser caches must discard every admin response.
public sealed class AdminCacheTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData("/admin/v1/test/cache", HttpStatusCode.OK)]
    [InlineData("/admin/v1/test/cache/private", HttpStatusCode.Unauthorized)]
    [InlineData("/admin/v1/test/cache/missing", HttpStatusCode.NotFound)]
    public async Task Given_an_admin_request_When_it_completes_Then_the_response_forbids_storage(string path, HttpStatusCode status)
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(status, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }
}

[ApiController]
[Route("admin/v1/test/cache")]
public sealed class AdminCacheProbeController : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Get() => Ok(new ProbeItem("example"));

    [HttpGet("private")]
    public IActionResult GetPrivate() => Ok(new ProbeItem("private"));
}
