using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using Keryx.Domain.Sources;
using Keryx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Keryx.Api.Tests.Sources;

// Acceptance Test
// Traces to: L2-001, L2-062
// Description: Staff list the source catalogue a page at a time, failing first, with counts as numbers.
// Each test builds its own catalogue in its own store, so counts are exact.
public sealed class ListSourcesTests
{
    private static readonly string[] ItemKeys =
    [
        "id", "kind", "address", "organization_name", "region", "region_name", "health", "failure_reason",
        "consecutive_failures", "held_count", "articles_30d", "last_fetched_at",
    ];

    [Fact]
    public async Task Given_sources_of_every_health_When_staff_list_them_Then_failing_come_first_then_needs_look_then_healthy_by_organization()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        // The seed adds one healthy news query: "Toronto church fitness".
        await AddAsync(factory, ("Zion Runners", SourceHealth.Healthy), ("Bethel Boxing", SourceHealth.Failing),
            ("Agape Yoga", SourceHealth.NeedsLook), ("Covenant Cycling", SourceHealth.Failing), ("Abide Strength", SourceHealth.Healthy));
        using var client = await StaffClient.OpenAsync(factory);

        var body = await client.GetFromJsonAsync<JsonElement>(new Uri("/admin/v1/sources?page=1&page_size=12", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(
            ["Bethel Boxing", "Covenant Cycling", "Agape Yoga", "Abide Strength", "Toronto church fitness", "Zion Runners"],
            body.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("organization_name").GetString()));
        Assert.Equal(6, body.GetProperty("total").GetInt32());
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(12, body.GetProperty("page_size").GetInt32());
        Assert.Equal(6, body.GetProperty("all_count").GetInt32());
        Assert.Equal(2, body.GetProperty("failing_count").GetInt32());
        Assert.Equal(1, body.GetProperty("needs_look_count").GetInt32());
        var first = body.GetProperty("items")[0];
        Assert.Equal(ItemKeys, first.EnumerateObject().Select(property => property.Name));
        Assert.Equal("failing", first.GetProperty("health").GetString());
        Assert.Equal("toronto", first.GetProperty("region").GetString());
        Assert.Equal("Toronto", first.GetProperty("region_name").GetString());
        Assert.Equal(JsonValueKind.Number, first.GetProperty("articles_30d").ValueKind);
        Assert.Equal(JsonValueKind.Null, first.GetProperty("last_fetched_at").ValueKind);
    }

    [Fact]
    public async Task Given_more_sources_than_a_page_When_the_second_page_is_read_Then_it_holds_the_rest_and_the_total()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await AddAsync(factory, [.. Enumerable.Range(1, 13).Select(i => ($"Org {i:00}", SourceHealth.Healthy))]);
        using var client = await StaffClient.OpenAsync(factory);

        var body = await client.GetFromJsonAsync<JsonElement>(new Uri("/admin/v1/sources?page=2&page_size=12", UriKind.Relative), TestContext.Current.CancellationToken);

        // 14 healthy by name: "Org 01".."Org 13", then "Toronto church fitness"; page 2 holds the last two.
        Assert.Equal(["Org 13", "Toronto church fitness"], body.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("organization_name").GetString()));
        Assert.Equal(14, body.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Given_a_kind_filter_When_listed_Then_only_that_kind_is_served_and_the_header_counts_cover_all()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await AddAsync(factory, ("Bethel Boxing", SourceHealth.Failing));
        using var client = await StaffClient.OpenAsync(factory);

        var body = await client.GetFromJsonAsync<JsonElement>(new Uri("/admin/v1/sources?kind=news_query&page=1&page_size=12", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(["Toronto church fitness"], body.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("organization_name").GetString()));
        Assert.Equal(1, body.GetProperty("total").GetInt32());
        Assert.Equal(2, body.GetProperty("all_count").GetInt32());
        Assert.Equal(1, body.GetProperty("failing_count").GetInt32());
    }

    [Theory]
    [InlineData("page=1&page_size=101", "invalid_page")]
    [InlineData("page=1&page_size=0", "invalid_page")]
    [InlineData("page=0&page_size=12", "invalid_page")]
    [InlineData("kind=podcast&page=1&page_size=12", "validation_failed")]
    public async Task Given_a_query_out_of_range_When_listed_Then_it_is_400_with_the_code(string query, string code)
    {
        using var factory = new ApiFactory();
        using var client = await StaffClient.OpenAsync(factory);

        using var response = await client.GetAsync(new Uri($"/admin/v1/sources?{query}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(code, problem.GetProperty("code").GetString());
    }

    private static async Task AddAsync(ApiFactory factory, params (string Organization, SourceHealth Health)[] sources)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
        var now = factory.Time.GetUtcNow();
        foreach (var (organization, health) in sources)
        {
            var source = Source.Add(SourceKind.Rss, $"https://{organization.Replace(' ', '-').ToLowerInvariant()}.example/feed", organization, "toronto", now);
            db.Sources.Add(source);
            // Health is derived by a run (journey 4); the test states it directly in the store.
            db.Entry(source).Property(candidate => candidate.Health).CurrentValue = health;
        }

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Equal(sources.Length + 1, await db.Sources.CountAsync(TestContext.Current.CancellationToken));
    }
}
