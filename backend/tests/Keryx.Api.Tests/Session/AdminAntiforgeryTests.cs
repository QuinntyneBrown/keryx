using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Keryx.Api.Tests.Session;

// L2-040. The same browser session needs both the anti-forgery cookie and its request token.
public sealed class AdminAntiforgeryTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task Given_a_session_without_a_request_token_When_it_changes_state_Then_it_is_refused(string method)
    {
        using var client = await TestSession.OpenAsync(factory, "staff-example", staff: true);
        using var request = new HttpRequestMessage(new HttpMethod(method), new Uri("/admin/v1/test/change", UriKind.Relative));
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("antiforgery_failed", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Given_a_session_When_it_sends_its_issued_token_Then_it_can_change_state()
    {
        using var client = await TestSession.OpenAsync(factory, "staff-example", staff: true);
        using var token = await client.GetAsync(new Uri("/admin/v1/auth/antiforgery", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, token.StatusCode);
        var body = await token.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/admin/v1/test/change", UriKind.Relative));
        request.Headers.Add("X-XSRF-TOKEN", body.GetProperty("request_token").GetString());
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

}

[ApiController]
[Route("admin/v1/test/change")]
public sealed class ChangeProbeController : ControllerBase
{
    [AcceptVerbs("POST", "PUT", "PATCH", "DELETE")]
    public IActionResult Change() => NoContent();
}
