using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Keryx.Api.Tests.Session;

public sealed class CurrentStaffTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Given_no_session_When_requesting_the_current_person_Then_authentication_is_required()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/admin/v1/me", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Given_a_non_staff_session_When_requesting_the_current_person_Then_it_returns_not_staff()
    {
        using var client = SessionClient([new Claim("sub", "non-staff")]);
        using var response = await client.GetAsync(new Uri("/admin/v1/me", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("not_staff", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Given_a_staff_session_When_requesting_the_current_person_Then_only_the_display_fields_are_returned()
    {
        await using var db = Fakes.TestStore.Open(factory.Store);
        db.StaffMembers.Add(new Keryx.Domain.Staff.StaffMember("subject-1", "Daniel Adeyemi", "Owner"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        using var client = SessionClient([new Claim("sub", "subject-1"), new Claim("roles", "KeryxStaff")]);
        using var response = await client.GetAsync(new Uri("/admin/v1/me", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(["initials", "name", "role"], me.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal("DA", me.GetProperty("initials").GetString());
        Assert.Equal("Daniel Adeyemi", me.GetProperty("name").GetString());
        Assert.Equal("Owner", me.GetProperty("role").GetString());
    }

    private HttpClient SessionClient(Claim[] claims)
    {
        var client = factory.CreateClient();
        const string scheme = CookieAuthenticationDefaults.AuthenticationScheme;
        var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(scheme);
        var identity = new ClaimsIdentity(claims, scheme);
        var cookie = options.TicketDataFormat.Protect(new AuthenticationTicket(new ClaimsPrincipal(identity), scheme));
        client.DefaultRequestHeaders.Add("Cookie", $"{options.Cookie.Name}={cookie}");
        return client;
    }
}
