using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace Keryx.Api.Tests.Session;

// L2-040. An endpoint with no explicit authorization metadata is private by default.
public sealed class EndpointProtectionTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Given_no_session_When_an_endpoint_has_no_policy_Then_it_refuses_the_request()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/test/private", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}

[ApiController]
[Route("test/private")]
public sealed class PrivateProbeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok();
}
