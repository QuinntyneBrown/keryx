using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using Keryx.Domain.Reference;
using Keryx.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using static Keryx.Api.Tests.Consumer.ConsumerApi;

namespace Keryx.Api.Tests.Consumer;

// Acceptance Test
// Traces to: L2-030 AC1, AC2
// Description: Any valid key reads the categories and the regions, each with its count of
// published articles as a number.
public sealed class ReferenceApiTests
{
    private static readonly DateTimeOffset Day = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    // "race" and "10K" make a race, "clinic" an event, a plain title faith & wellness; the bake
    // sale names no topic and is held, so it counts nowhere.
    private static async Task<HttpClient> SeededAsync(ApiFactory factory)
    {
        await StaffClient.SeedAsync(factory);
        await CollectAsync(factory, "https://redeemedrun.ca/feed", "Redeemed Run Club", "toronto",
            new Item("Running birch 5K race", "https://redeemedrun.ca/n/birch", Day),
            new Item("Running apple", "https://redeemedrun.ca/n/apple", Day),
            new Item("Running clinic downtown", "https://redeemedrun.ca/n/clinic", Day),
            new Item("Community bake sale", "https://redeemedrun.ca/n/bake", Day));
        await CollectAsync(factory, "https://lifepointfit.ca/feed", "LifePoint Fitness", "mississauga",
            new Item("Running delta 10K race", "https://lifepointfit.ca/n/delta", Day));
        // Any valid key: this one has none of the scopes the other endpoints need.
        return Client(factory, await KeyAsync(factory, "events:read"));
    }

    [Fact]
    public async Task Given_any_valid_key_When_categories_are_read_Then_the_six_come_in_order_each_with_its_published_count()
    {
        using var factory = new ApiFactory();
        using var client = await SeededAsync(factory);

        var data = (await ReadAsync(client, "/v1/categories")).GetProperty("data").EnumerateArray().ToList();

        Assert.Equal(
            [("events", "Events", 1), ("races-challenges", "Races & Challenges", 2), ("gyms-studios", "Gyms & Studios", 0),
             ("ministry-programs", "Ministry Programs", 0), ("nutrition", "Nutrition", 0), ("faith-wellness", "Faith & Wellness", 1)],
            data.Select(item => (item.GetProperty("slug").GetString(), item.GetProperty("name").GetString(), item.GetProperty("article_count").GetInt32())));
        Assert.All(data, item => Assert.Equal(["article_count", "name", "slug"], item.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)));
    }

    [Fact]
    public async Task Given_any_valid_key_When_regions_are_read_Then_Toronto_comes_first_and_global_last_each_with_its_zone_and_published_count()
    {
        using var factory = new ApiFactory();
        using var client = await SeededAsync(factory);

        var data = (await ReadAsync(client, "/v1/regions")).GetProperty("data").EnumerateArray().ToList();

        Assert.Equal(
            ["toronto", "scarborough", "mississauga", "brampton", "markham", "vaughan", "oakville", "richmond-hill", "pickering", "ajax", "gta", "ontario", "canada", "global"],
            data.Select(item => item.GetProperty("slug").GetString()));
        Assert.All(data, item => Assert.Equal(["article_count", "name", "slug", "time_zone"], item.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)));
        Assert.Equal(("Toronto", "America/Toronto", 3), (data[0].GetProperty("name").GetString(), data[0].GetProperty("time_zone").GetString(), data[0].GetProperty("article_count").GetInt32()));
        Assert.Equal(1, data[2].GetProperty("article_count").GetInt32());
        Assert.Equal(("Global", "Etc/UTC", 0), (data[^1].GetProperty("name").GetString(), data[^1].GetProperty("time_zone").GetString(), data[^1].GetProperty("article_count").GetInt32()));
    }

    [Fact]
    public async Task Given_more_than_100_regions_When_regions_are_read_Then_the_first_100_by_rank_are_returned()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
            for (var i = 0; i < 100; i++)
            {
                db.Regions.Add(new Region($"area-{i:D3}", $"Area {i:D3}", "America/Toronto", 200 + i));
            }

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var client = Client(factory, await KeyAsync(factory, "events:read"));
        var data = (await ReadAsync(client, "/v1/regions")).GetProperty("data").EnumerateArray().ToList();

        Assert.Equal(100, data.Count);
        Assert.Equal(("toronto", "area-089"), (data[0].GetProperty("slug").GetString(), data[^1].GetProperty("slug").GetString()));
    }

    [Theory]
    [InlineData("/v1/categories")]
    [InlineData("/v1/regions")]
    [InlineData("/v1/status")]
    public async Task Given_no_valid_key_When_reference_data_is_read_Then_401_invalid_api_key(string path)
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        using var client = Client(factory, "kx_live_ABCDEFGHIJKLMNOPQRSTUVWX");

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal((HttpStatusCode.Unauthorized, "invalid_api_key"), (response.StatusCode, problem.GetProperty("code").GetString()));
    }

    private static async Task<JsonElement> ReadAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }
}
