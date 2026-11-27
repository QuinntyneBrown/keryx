using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;

namespace Keryx.Api.Tests.Sources;

// Acceptance Test
// Traces to: L2-001, L2-018
// Description: Staff read the region list the add-source dialog offers, Toronto first.
public sealed class AdminRegionsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly Uri Regions = new("/admin/v1/regions", UriKind.Relative);

    [Fact]
    public async Task Given_the_seeded_regions_When_staff_read_them_Then_they_come_in_rank_order_with_slug_name_and_zone()
    {
        await StaffClient.SeedAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);

        var body = await client.GetFromJsonAsync<JsonElement>(Regions, TestContext.Current.CancellationToken);

        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(
            ["toronto", "scarborough", "mississauga", "brampton", "markham", "vaughan", "oakville", "richmond-hill", "pickering", "ajax", "gta", "ontario", "canada", "global"],
            items.Select(item => item.GetProperty("slug").GetString()));
        Assert.Equal(["slug", "name", "time_zone"], items[0].EnumerateObject().Select(property => property.Name));
        Assert.Equal("Richmond Hill", items[7].GetProperty("name").GetString());
        Assert.Equal("Etc/UTC", items[13].GetProperty("time_zone").GetString());
    }

    [Fact]
    public async Task Given_no_session_When_the_regions_are_read_Then_it_is_401()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        using var response = await client.GetAsync(Regions, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
