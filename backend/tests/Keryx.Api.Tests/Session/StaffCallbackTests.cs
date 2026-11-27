using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Keryx.Api.Tests.Session;

public sealed class StaffCallbackTests(StaffProviderFactory factory) : IClassFixture<StaffProviderFactory>
{
    [Fact]
    public async Task Given_a_staff_identity_When_the_provider_returns_Then_the_browser_can_read_its_profile_until_idle_expiry()
    {
        // The browser's cookies are carried by hand. A cookie container would judge each cookie's
        // expiry by the real clock, while the API sets it by the fixture's fake clock, so the
        // correlation cookie would vanish whenever the real time passed the fake one.
        using var browser = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
        var ct = TestContext.Current.CancellationToken;
        using var challenge = await browser.GetAsync(new Uri("/admin/v1/auth/sign-in?return=/articles", UriKind.Relative), ct);
        var parameters = QueryHelpers.ParseQuery(challenge.Headers.Location!.Query);
        factory.Provider.Nonce = parameters["nonce"].ToString();
        var callbackPath = QueryHelpers.AddQueryString("/admin/v1/auth/callback", "state", parameters["state"].ToString());
        using var callbackRequest = new HttpRequestMessage(HttpMethod.Get, new Uri(callbackPath + "&code=example-code", UriKind.Relative));
        callbackRequest.Headers.Add("Cookie", CookiesFrom(challenge));
        using var callback = await browser.SendAsync(callbackRequest, ct);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/articles", callback.Headers.Location?.OriginalString);
        var cookie = Assert.Single(callback.Headers.GetValues("Set-Cookie"), value => value.StartsWith(".AspNetCore.Cookies=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        var endpoint = new Uri("/admin/v1/me", UriKind.Relative);
        browser.DefaultRequestHeaders.Add("Cookie", CookiesFrom(callback));
        using var me = await browser.GetAsync(endpoint, ct);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var profile = await me.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("Daniel Adeyemi", profile.GetProperty("name").GetString());
        factory.Time.Advance(TimeSpan.FromMinutes(60));
        using var expired = await browser.GetAsync(endpoint, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        var problem = await expired.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("session_expired", problem.GetProperty("code").GetString());
    }

    /// <summary>The <c>name=value</c> pairs a response sets, as one <c>Cookie</c> header.</summary>
    private static string CookiesFrom(HttpResponseMessage response) =>
        string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(cookie => cookie.Split(';', 2)[0]));
}

public sealed class StaffProviderFactory : ApiFactory
{
    public ProviderBackchannel Provider { get; } = new();

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
        {
            options.Configuration!.TokenEndpoint = "https://id.example.test/token";
            options.Configuration.SigningKeys.Add(Provider.Key);
            options.Backchannel = new HttpClient(Provider, disposeHandler: false);
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Provider.Dispose();
        }

        base.Dispose(disposing);
    }
}

public sealed class ProviderBackchannel : HttpMessageHandler
{
    private readonly RSA _rsa = RSA.Create(2048);
    public string Nonce { private get; set; } = string.Empty;
    public RsaSecurityKey Key => new(_rsa) { KeyId = "test-issuer" };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Assert.Equal("https://id.example.test/token", request.RequestUri?.AbsoluteUri);
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "https://id.example.test",
            Audience = "keryx-test",
            SigningCredentials = new SigningCredentials(Key, SecurityAlgorithms.RsaSha256),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = "subject-42",
                ["name"] = "Daniel Adeyemi",
                ["roles"] = "KeryxStaff",
                ["nonce"] = Nonce
            },
        });
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { id_token = token, access_token = "provider-access", token_type = "Bearer" }),
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _rsa.Dispose();
        }

        base.Dispose(disposing);
    }
}
