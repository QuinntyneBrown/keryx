using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using static Keryx.Api.Tests.Consumer.ConsumerApi;

namespace Keryx.Api.Tests.Consumer;

// Acceptance Test
// Traces to: L2-029 AC1-AC3
// Description: A product lists the organizations that have something published, by name, a page
// at a time, filtered by region, kind and a name prefix, and reads one by id.
public sealed class OrganizationsApiTests
{
    private static readonly DateTimeOffset Day = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_organizations_with_and_without_published_articles_When_listed_Then_only_listed_ones_come_by_name_with_their_fields_and_served_count()
    {
        using var factory = new ApiFactory();
        using var client = await SeededAsync(factory);

        var page = await PageAsync(client, string.Empty);

        Assert.Equal(["Bethel Running Ministry", "LifePoint Fitness", "Redeemed Run Club"], Names(page));
        var lifePoint = page.GetProperty("data")[1];
        Assert.Equal(
            ["article_count", "id", "kind", "name", "region", "website_url"],
            lifePoint.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(
            (await OrganizationIdAsync(factory, "LifePoint Fitness"), "gym", "mississauga", "https://lifepointfit.ca", 2),
            (lifePoint.GetProperty("id").GetString(), lifePoint.GetProperty("kind").GetString(), lifePoint.GetProperty("region").GetString(),
             lifePoint.GetProperty("website_url").GetString(), lifePoint.GetProperty("article_count").GetInt32()));
        // Its held article is not counted.
        Assert.Equal(("club", 1), (page.GetProperty("data")[2].GetProperty("kind").GetString(), page.GetProperty("data")[2].GetProperty("article_count").GetInt32()));
    }

    [Fact]
    public async Task Given_three_listed_organizations_When_listed_one_at_a_time_Then_each_comes_once_and_the_last_page_has_no_cursor()
    {
        using var factory = new ApiFactory();
        using var client = await SeededAsync(factory);

        var first = await PageAsync(client, "limit=1");
        var second = await PageAsync(client, $"limit=1&cursor={Uri.EscapeDataString(Cursor(first)!)}");
        var third = await PageAsync(client, $"limit=1&cursor={Uri.EscapeDataString(Cursor(second)!)}");

        Assert.Equal(["Bethel Running Ministry", "LifePoint Fitness", "Redeemed Run Club"], new[] { first, second, third }.SelectMany(Names));
        Assert.Equal(JsonValueKind.Null, third.GetProperty("next_cursor").ValueKind);
    }

    public static TheoryData<string, string[]> Filters => new()
    {
        { "region=toronto", ["Bethel Running Ministry", "Redeemed Run Club"] },
        { "kind=gym", ["LifePoint Fitness"] },
        { "kind=ministry&region=toronto", ["Bethel Running Ministry"] },
        { "q=li", ["LifePoint Fitness"] },
        { "q=RED", ["Redeemed Run Club"] },
        // A name prefix is matched as names are: case and a leading "The" ignored.
        { "q=The%20Redeemed", ["Redeemed Run Club"] },
        // An organization with nothing published is not listed, so no filter finds it.
        { "q=grace", [] },
        // A prefix, not anywhere in the name.
        { "q=point", [] },
        { "kind=church", [] },
    };

    [Theory]
    [MemberData(nameof(Filters))]
    public async Task Given_a_region_kind_or_name_prefix_When_it_filters_Then_only_matching_listed_organizations_return(string query, string[] names)
    {
        using var factory = new ApiFactory();
        using var client = await SeededAsync(factory);

        Assert.Equal(names, Names(await PageAsync(client, query)));
    }

    [Theory]
    [InlineData("kind=temple", "invalid_filter")]
    [InlineData("kind=Gym", "invalid_filter")]
    [InlineData("region=atlantis", "invalid_filter")]
    [InlineData("q=l", "invalid_filter")]
    [InlineData("q=abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghi", "invalid_filter")]
    [InlineData("limit=0", "invalid_limit")]
    [InlineData("cursor=abc", "invalid_cursor")]
    public async Task Given_a_value_it_cannot_use_When_organizations_are_listed_Then_400_with_its_code(string query, string code)
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        using var client = Client(factory, await KeyAsync(factory, "sources:read"));

        using var response = await client.GetAsync(new Uri($"/v1/organizations?{query}", UriKind.Relative), TestContext.Current.CancellationToken);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal((HttpStatusCode.BadRequest, code), (response.StatusCode, problem.GetProperty("code").GetString()));
    }

    [Fact]
    public async Task Given_a_cursor_When_it_is_sent_with_other_filters_Then_400_invalid_cursor()
    {
        using var factory = new ApiFactory();
        using var client = await SeededAsync(factory);
        var cursor = Uri.EscapeDataString(Cursor(await PageAsync(client, "limit=1&region=toronto"))!);

        using var response = await client.GetAsync(new Uri($"/v1/organizations?limit=1&region=toronto&q=re&cursor={cursor}", UriKind.Relative), TestContext.Current.CancellationToken);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal((HttpStatusCode.BadRequest, "invalid_cursor"), (response.StatusCode, problem.GetProperty("code").GetString()));
        Assert.Equal(["Redeemed Run Club"], Names(await PageAsync(client, $"limit=1&region=toronto&cursor={cursor}")));
    }

    [Fact]
    public async Task Given_a_name_prefix_of_60_characters_When_it_filters_Then_it_is_accepted()
    {
        using var factory = new ApiFactory();
        using var client = await SeededAsync(factory);

        Assert.Empty(Names(await PageAsync(client, $"q={new string('a', 60)}")));
    }

    [Fact]
    public async Task Given_a_listed_organization_When_it_is_read_by_id_Then_it_has_the_same_fields_as_in_the_list()
    {
        using var factory = new ApiFactory();
        using var client = await SeededAsync(factory);
        var listed = (await PageAsync(client, "kind=gym")).GetProperty("data")[0];

        using var response = await client.GetAsync(new Uri($"/v1/organizations/{await OrganizationIdAsync(factory, "LifePoint Fitness")}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(listed.GetRawText(), (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetRawText());
    }

    [Theory]
    [InlineData("unlisted")]
    [InlineData("org_aaaaaaaaaaaa")]
    [InlineData("nonsense")]
    public async Task Given_an_organization_that_is_not_listed_or_does_not_exist_When_it_is_read_Then_404_organization_not_found(string which)
    {
        using var factory = new ApiFactory();
        using var client = await SeededAsync(factory);
        var id = which == "unlisted" ? await OrganizationIdAsync(factory, "Grace Community Church") : which;

        using var response = await client.GetAsync(new Uri($"/v1/organizations/{id}", UriKind.Relative), TestContext.Current.CancellationToken);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal((HttpStatusCode.NotFound, "organization_not_found"), (response.StatusCode, problem.GetProperty("code").GetString()));
    }

    [Theory]
    [InlineData("/v1/organizations")]
    [InlineData("/v1/organizations/org_aaaaaaaaaaaa")]
    public async Task Given_a_key_without_sources_read_When_organizations_are_requested_Then_403_insufficient_scope(string path)
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        using var client = Client(factory, await KeyAsync(factory, "articles:read", "events:read"));

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal((HttpStatusCode.Forbidden, "insufficient_scope"), (response.StatusCode, problem.GetProperty("code").GetString()));
    }

    // "running" is one topic, so a title naming it is published in a focus area; a bake sale is held.
    private static async Task<HttpClient> SeededAsync(ApiFactory factory)
    {
        await StaffClient.SeedAsync(factory);
        await CollectAsync(factory, "https://redeemedrun.ca/feed", "Redeemed Run Club", "toronto",
            new Item("Running apple", "https://redeemedrun.ca/n/apple", Day),
            new Item("Community bake sale", "https://redeemedrun.ca/n/bake", Day));
        await CollectAsync(factory, "https://lifepointfit.ca/feed", "LifePoint Fitness", "mississauga",
            new Item("Running birch", "https://lifepointfit.ca/n/birch", Day),
            new Item("Running cedar", "https://lifepointfit.ca/n/cedar", Day));
        await CollectAsync(factory, "https://bethelrun.org/feed", "Bethel Running Ministry", "toronto",
            new Item("Running delta", "https://bethelrun.org/n/delta", Day));
        await CollectAsync(factory, "https://gracecommunity.ca/feed", "Grace Community Church", "toronto",
            new Item("Community bake sale this Saturday", "https://gracecommunity.ca/n/bake", Day));
        return Client(factory, await KeyAsync(factory, "sources:read"));
    }

    private static IEnumerable<string?> Names(JsonElement page) =>
        page.GetProperty("data").EnumerateArray().Select(item => item.GetProperty("name").GetString());

    private static string? Cursor(JsonElement page) => page.GetProperty("next_cursor").GetString();

    private static async Task<JsonElement> PageAsync(HttpClient client, string query)
    {
        using var response = await client.GetAsync(new Uri($"/v1/organizations?{query}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }
}
