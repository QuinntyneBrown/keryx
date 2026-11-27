using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using Keryx.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Keryx.Api.Tests.Sources;

// Acceptance Test
// Traces to: L2-001
// Description: The same source cannot be added twice, however its address is written.
public sealed class DuplicateSourceTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly Uri Sources = new("/admin/v1/sources", UriKind.Relative);

    // L2-001 AC3 and AC4.
    [Theory]
    [InlineData("https://gracechurch.ca/events/feed", "https://gracechurch.ca/events/feed")]
    [InlineData("https://gracechurch.ca/news", "HTTPS://GraceChurch.CA/news/#top")]
    public async Task Given_an_existing_source_When_the_same_kind_and_normalized_address_is_added_Then_it_is_409_source_exists(
        string existing, string again)
    {
        await StaffClient.SeedAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);
        using var first = await client.PostAsJsonAsync(Sources, Body("rss", existing), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var before = await CountAsync();

        using var second = await client.PostAsJsonAsync(Sources, Body("rss", again), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var problem = await second.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("source_exists", problem.GetProperty("code").GetString());
        Assert.Equal(before, await CountAsync());
    }

    [Fact]
    public async Task Given_an_existing_source_When_the_same_address_is_added_as_another_kind_Then_it_is_created()
    {
        await StaffClient.SeedAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);
        using var rss = await client.PostAsJsonAsync(Sources, Body("rss", "https://gracechurch.ca/both"), TestContext.Current.CancellationToken);

        using var website = await client.PostAsJsonAsync(Sources, Body("website", "https://gracechurch.ca/both"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, rss.StatusCode);
        Assert.Equal(HttpStatusCode.Created, website.StatusCode);
    }

    private static object Body(string kind, string address) =>
        new { kind, address, organization_name = "Grace Church", region = "toronto" };

    private async Task<int> CountAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAppDbContext>().Sources.CountAsync(TestContext.Current.CancellationToken);
    }
}
