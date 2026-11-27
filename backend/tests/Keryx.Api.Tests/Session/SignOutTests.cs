using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Keryx.Api.Tests.Session;

public sealed class SignOutTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Given_a_staff_session_When_signing_out_Then_even_the_previous_cookie_loses_access()
    {
        using var browser = await TestSession.OpenAsync(factory, "signing-out", staff: true);
        var ct = TestContext.Current.CancellationToken;
        var antiforgery = new Uri("/admin/v1/auth/antiforgery", UriKind.Relative);
        var token = await browser.GetFromJsonAsync<JsonElement>(antiforgery, ct);
        using var signOut = new HttpRequestMessage(HttpMethod.Post, new Uri("/admin/v1/auth/sign-out", UriKind.Relative));
        signOut.Headers.Add("X-XSRF-TOKEN", token.GetProperty("request_token").GetString());
        using var ended = await browser.SendAsync(signOut, ct);
        Assert.Equal(HttpStatusCode.NoContent, ended.StatusCode);
        Assert.Contains(ended.Headers.GetValues("Set-Cookie"), value => value.StartsWith(".AspNetCore.Cookies=;", StringComparison.Ordinal));
        // The fixture deliberately retains the original credential in its request headers.
        using var replay = await browser.GetAsync(antiforgery, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }
}
