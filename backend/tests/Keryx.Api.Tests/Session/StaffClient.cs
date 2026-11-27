using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Application.Seeding;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Keryx.Api.Tests.Session;

/// <summary>A signed-in staff member's browser: the session cookie and the anti-forgery token.</summary>
internal static class StaffClient
{
    /// <summary>
    /// Opens a staff session and sends its anti-forgery request token on every request, as the
    /// admin app does on state-changing requests.
    /// </summary>
    public static async Task<HttpClient> OpenAsync(ApiFactory factory, string subject = "staff-example")
    {
        var client = await TestSession.OpenAsync(factory, subject, staff: true);
        var body = await client.GetFromJsonAsync<JsonElement>(
            new Uri("/admin/v1/auth/antiforgery", UriKind.Relative), TestContext.Current.CancellationToken);
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", body.GetProperty("request_token").GetString());
        return client;
    }

    /// <summary>Adds the seed data (regions, categories, settings) to the fixture's store.</summary>
    public static async Task SeedAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new SeedDataCommand(), TestContext.Current.CancellationToken);
    }
}
