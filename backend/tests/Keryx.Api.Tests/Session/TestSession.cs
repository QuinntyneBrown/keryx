using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace Keryx.Api.Tests.Session;

internal static class TestSession
{
    public static async Task<HttpClient> OpenAsync(ApiFactory factory, string subject, bool staff = false)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Scheme = "https";
        const string scheme = CookieAuthenticationDefaults.AuthenticationScheme;
        var identity = new ClaimsIdentity([new Claim("sub", subject), new Claim(ClaimTypes.NameIdentifier, subject)], scheme);
        if (staff)
        {
            identity.AddClaim(new Claim("roles", "KeryxStaff"));
        }
        await context.SignInAsync(scheme, new ClaimsPrincipal(identity));
        var cookie = SetCookieHeaderValue.Parse(context.Response.Headers.SetCookie.Single());
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Add("Cookie", $"{cookie.Name}={cookie.Value}");
        return client;
    }
}
