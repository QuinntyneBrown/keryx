using System.Diagnostics.Metrics;
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

    // L2-085 AC3: a callback refused for its identity is a failed sign-in, counted by route only.
    [Fact]
    public async Task Given_an_identity_with_a_subject_too_long_When_the_provider_returns_Then_sign_in_fails_and_is_counted()
    {
        using var browser = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
        var ct = TestContext.Current.CancellationToken;
        var refusals = new List<(string? Reason, string? Route)>();
        var meters = factory.Services.GetRequiredService<IMeterFactory>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, listening) =>
            {
                if (instrument.Name == "keryx.auth.refused" && instrument.Meter.Scope == meters)
                {
                    listening.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            var named = tags.ToArray().ToDictionary(tag => tag.Key, tag => $"{tag.Value}", StringComparer.Ordinal);
            lock (refusals)
            {
                refusals.Add((named["reason"], named["http.route"]));
            }
        });
        listener.Start();
        factory.Provider.Subject = new string('s', 256);
        try
        {
            using var challenge = await browser.GetAsync(new Uri("/admin/v1/auth/sign-in?return=/articles", UriKind.Relative), ct);
            var parameters = QueryHelpers.ParseQuery(challenge.Headers.Location!.Query);
            factory.Provider.Nonce = parameters["nonce"].ToString();
            var callbackPath = QueryHelpers.AddQueryString("/admin/v1/auth/callback", "state", parameters["state"].ToString());
            using var callbackRequest = new HttpRequestMessage(HttpMethod.Get, new Uri(callbackPath + "&code=example-code", UriKind.Relative));
            callbackRequest.Headers.Add("Cookie", CookiesFrom(challenge));
            using var callback = await browser.SendAsync(callbackRequest, ct);

            Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
            Assert.StartsWith("/sign-in?state=error", callback.Headers.Location?.OriginalString, StringComparison.Ordinal);
            Assert.Equal([("signin_failed", "admin/v1/auth/callback")], refusals);
        }
        finally
        {
            factory.Provider.Subject = "subject-42";
        }
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

    /// <summary>The identity's subject; a test may make it one the callback refuses.</summary>
    public string Subject { private get; set; } = "subject-42";
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
                ["sub"] = Subject,
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
