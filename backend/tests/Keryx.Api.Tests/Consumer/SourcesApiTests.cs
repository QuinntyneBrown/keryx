using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Runs;
using Keryx.Api.Tests.Session;
using Keryx.Domain.Sources;
using static Keryx.Api.Tests.Consumer.ConsumerApi;

namespace Keryx.Api.Tests.Consumer;

// Acceptance Test
// Traces to: L2-029 AC4
// Description: A product lists the sources Keryx collects from as name, kind, organization and
// region only: never a source's health, its failure, its address or a news query's text.
public sealed class SourcesApiTests
{
    private static readonly DateTimeOffset Day = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_healthy_failing_and_news_query_sources_When_listed_Then_each_comes_by_name_with_its_four_public_fields_only()
    {
        using var factory = new ApiFactory();
        using var client = await SeededAsync(factory);

        using var response = await client.GetAsync(new Uri("/v1/sources", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var page = JsonDocument.Parse(text).RootElement;

        Assert.Equal(["Broken Feed Club", "Grace Community Church", "LifePoint Fitness", "Toronto church fitness"], Names(page));
        foreach (var source in page.GetProperty("data").EnumerateArray())
        {
            Assert.Equal(["kind", "name", "organization", "region"], source.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        }

        var lifePoint = page.GetProperty("data")[2];
        Assert.Equal(
            ("rss", "mississauga", await OrganizationIdAsync(factory, "LifePoint Fitness"), "LifePoint Fitness"),
            (lifePoint.GetProperty("kind").GetString(), lifePoint.GetProperty("region").GetString(),
             lifePoint.GetProperty("organization").GetProperty("id").GetString(), lifePoint.GetProperty("organization").GetProperty("name").GetString()));
        var news = page.GetProperty("data")[3];
        Assert.Equal(("news_query", "gta"), (news.GetProperty("kind").GetString(), news.GetProperty("region").GetString()));
        // An organization with nothing published is not listed, so its source names none.
        Assert.Equal(JsonValueKind.Null, page.GetProperty("data")[1].GetProperty("organization").ValueKind);
        foreach (var secret in new[] { "near:Toronto", "https://brokenfeed.ca/feed", "https://lifepointfit.ca/feed", "not_found", "failing", "healthy" })
        {
            Assert.DoesNotContain(secret, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Given_four_sources_When_listed_two_at_a_time_Then_each_comes_once_and_the_last_page_has_no_cursor()
    {
        using var factory = new ApiFactory();
        using var client = await SeededAsync(factory);

        var first = await PageAsync(client, "limit=2");
        var second = await PageAsync(client, $"limit=2&cursor={Uri.EscapeDataString(first.GetProperty("next_cursor").GetString()!)}");

        Assert.Equal(["Broken Feed Club", "Grace Community Church", "LifePoint Fitness", "Toronto church fitness"], Names(first).Concat(Names(second)));
        Assert.Equal(JsonValueKind.Null, second.GetProperty("next_cursor").ValueKind);
    }

    [Theory]
    [InlineData("limit=0", "invalid_limit")]
    [InlineData("limit=101", "invalid_limit")]
    [InlineData("cursor=abc", "invalid_cursor")]
    public async Task Given_a_value_it_cannot_use_When_sources_are_listed_Then_400_with_its_code(string query, string code)
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        using var client = Client(factory, await KeyAsync(factory, "sources:read"));

        using var response = await client.GetAsync(new Uri($"/v1/sources?{query}", UriKind.Relative), TestContext.Current.CancellationToken);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal((HttpStatusCode.BadRequest, code), (response.StatusCode, problem.GetProperty("code").GetString()));
    }

    [Fact]
    public async Task Given_an_organizations_cursor_When_it_is_sent_to_sources_Then_400_invalid_cursor()
    {
        using var factory = new ApiFactory();
        using var client = await SeededAsync(factory);
        await CollectAsync(factory, "https://redeemedrun.ca/feed", "Redeemed Run Club", "toronto", new Item("Running apple", "https://redeemedrun.ca/n/apple", Day));
        using var organizations = await client.GetAsync(new Uri("/v1/organizations?limit=1", UriKind.Relative), TestContext.Current.CancellationToken);
        var cursor = (await organizations.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("next_cursor").GetString()!;

        using var response = await client.GetAsync(new Uri($"/v1/sources?cursor={Uri.EscapeDataString(cursor)}", UriKind.Relative), TestContext.Current.CancellationToken);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal((HttpStatusCode.BadRequest, "invalid_cursor"), (response.StatusCode, problem.GetProperty("code").GetString()));
    }

    [Fact]
    public async Task Given_a_key_without_sources_read_When_sources_are_requested_Then_403_insufficient_scope()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        using var client = Client(factory, await KeyAsync(factory, "articles:read", "events:read"));

        using var response = await client.GetAsync(new Uri("/v1/sources", UriKind.Relative), TestContext.Current.CancellationToken);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal((HttpStatusCode.Forbidden, "insufficient_scope"), (response.StatusCode, problem.GetProperty("code").GetString()));
    }

    // The seed adds the "Toronto church fitness" news query; nothing answers it or the broken feed, so both fail.
    private static async Task<HttpClient> SeededAsync(ApiFactory factory)
    {
        await StaffClient.SeedAsync(factory);
        await CollectionRuns.AddSourceAsync(factory, SourceKind.Rss, "https://brokenfeed.ca/feed", "Broken Feed Club", "toronto");
        await CollectAsync(factory, "https://lifepointfit.ca/feed", "LifePoint Fitness", "mississauga", new Item("Running birch", "https://lifepointfit.ca/n/birch", Day));
        await CollectAsync(factory, "https://gracecommunity.ca/feed", "Grace Community Church", "toronto", new Item("Community bake sale", "https://gracecommunity.ca/n/bake", Day));
        return Client(factory, await KeyAsync(factory, "sources:read"));
    }

    private static IEnumerable<string?> Names(JsonElement page) =>
        page.GetProperty("data").EnumerateArray().Select(item => item.GetProperty("name").GetString());

    private static async Task<JsonElement> PageAsync(HttpClient client, string query)
    {
        using var response = await client.GetAsync(new Uri($"/v1/sources?{query}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }
}
