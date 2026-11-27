using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;

namespace Keryx.Api.Tests.Session;

// L2-039: the provider authenticates staff with a one-time code and a PKCE challenge.
public sealed class SignInTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Given_an_account_switch_When_sign_in_starts_Then_the_provider_is_asked_to_select_an_account()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        using var response = await client.GetAsync(new Uri("/admin/v1/auth/sign-in?prompt=select_account", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("select_account", QueryHelpers.ParseQuery(response.Headers.Location!.Query)["prompt"].ToString());
    }

    [Fact]
    public async Task Given_a_visitor_When_they_start_sign_in_Then_the_provider_receives_a_bound_code_challenge()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        using var response = await client.GetAsync(new Uri("/admin/v1/auth/sign-in?return=/articles", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var redirect = Assert.IsType<Uri>(response.Headers.Location);
        Assert.Equal("https://id.example.test/authorize", redirect.GetLeftPart(UriPartial.Path));
        var query = QueryHelpers.ParseQuery(redirect.Query);
        Assert.Equal("code", query["response_type"].ToString());
        Assert.Equal("S256", query["code_challenge_method"].ToString());
        Assert.Equal("https://localhost/admin/v1/auth/callback", query["redirect_uri"].ToString());
        foreach (var key in new[] { "code_challenge", "state", "nonce" })
        {
            Assert.False(string.IsNullOrWhiteSpace(query[key]));
        }
    }
}
