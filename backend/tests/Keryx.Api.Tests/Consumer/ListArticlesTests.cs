using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using static Keryx.Api.Tests.Consumer.ConsumerApi;

namespace Keryx.Api.Tests.Consumer;

// Acceptance Test
// Traces to: L2-026 AC1, AC2, AC5, AC6, AC7
// Description: A product lists published articles newest first, a page at a time, following an
// opaque cursor; held and hidden articles never appear.
public sealed class ListArticlesTests
{
    private const string Toronto = "https://redeemedrun.ca/feed";
    private const string Global = "https://worldfaithfit.org/feed";
    private static readonly DateTimeOffset Newest = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    private static readonly string[] Words =
    [
        "apple", "birch", "cedar", "delta", "ember", "fjord", "grove", "harbor", "island", "juniper",
        "kestrel", "lantern", "meadow", "nectar", "orchard", "prairie", "quartz", "river", "summit", "tundra",
        "upland", "valley", "willow", "yarrow", "zephyr",
    ];

    // In Toronto, "running" is one topic: each scores 0.60 and is published at the default 0.60.
    // Three share each publication time, so a page boundary falls inside a tie.
    private static Item[] TwentyFive() =>
        [.. Words.Select((word, i) => new Item($"Running {word}", $"https://redeemedrun.ca/n/{word}", Newest.AddHours(-(i / 3))))];

    [Fact]
    public async Task Given_25_published_articles_When_listed_10_at_a_time_Then_three_pages_hold_each_once_newest_first_and_the_last_has_no_cursor()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await CollectAsync(factory, Toronto, "Redeemed Run Club", "toronto", TwentyFive());
        using var client = Client(factory, await KeyAsync(factory, "articles:read"));

        var first = await PageAsync(client, "limit=10");
        var second = await PageAsync(client, $"limit=10&cursor={Uri.EscapeDataString(Cursor(first)!)}");
        var third = await PageAsync(client, $"limit=10&cursor={Uri.EscapeDataString(Cursor(second)!)}");

        Assert.Equal([10, 10, 5], new[] { first, second, third }.Select(page => page.GetProperty("data").GetArrayLength()));
        Assert.Equal(JsonValueKind.Null, third.GetProperty("next_cursor").ValueKind);
        var all = new[] { first, second, third }.SelectMany(page => page.GetProperty("data").EnumerateArray()).ToList();
        Assert.Equal(25, all.Select(item => item.GetProperty("id").GetString()).Distinct(StringComparer.Ordinal).Count());
        var times = all.Select(item => item.GetProperty("published_at").GetDateTimeOffset()).ToList();
        Assert.Equal(times.OrderDescending(), times);
        Assert.Equal(Newest, times[0]);
        Assert.Equal(Newest.AddHours(-8), times[^1]);
        // One page of 100 is the same sequence: the order does not depend on the page size.
        var whole = await PageAsync(client, "limit=100");
        Assert.Equal(all.Select(Id), whole.GetProperty("data").EnumerateArray().Select(Id));
    }

    [Theory]
    [InlineData("", 20)]
    [InlineData("limit=1", 1)]
    [InlineData("limit=100", 25)]
    public async Task Given_25_published_articles_When_a_limit_is_given_or_not_Then_that_many_at_most_are_returned(string query, int count)
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await CollectAsync(factory, Toronto, "Redeemed Run Club", "toronto", TwentyFive());
        using var client = Client(factory, await KeyAsync(factory, "articles:read"));

        var page = await PageAsync(client, query);

        Assert.Equal(count, page.GetProperty("data").GetArrayLength());
    }

    [Theory]
    [InlineData("limit=0")]
    [InlineData("limit=101")]
    [InlineData("limit=ten")]
    [InlineData("limit=-1")]
    [InlineData("limit=1.5")]
    public async Task Given_a_limit_out_of_range_or_not_a_number_When_listed_Then_400_invalid_limit(string query)
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        using var client = Client(factory, await KeyAsync(factory, "articles:read"));

        using var response = await client.GetAsync(new Uri($"/v1/articles?{query}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("invalid_limit", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Given_held_and_hidden_articles_When_listed_Then_only_published_ones_appear_each_with_the_list_fields_alone()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await CollectAsync(factory, Toronto, "Redeemed Run Club", "toronto",
            new Item("Sunrise running group meets weekly", "https://redeemedrun.ca/n/1", Newest),
            new Item("Community bake sale this Saturday", "https://redeemedrun.ca/n/2", Newest));
        await CollectAsync(factory, Global, "World Faith Fit", "global", new Item("Running and faith retreat", "https://worldfaithfit.org/1", Newest));
        using var client = Client(factory, await KeyAsync(factory, "articles:read"));

        var page = await PageAsync(client, string.Empty);

        var item = Assert.Single(page.GetProperty("data").EnumerateArray());
        Assert.Equal("Sunrise running group meets weekly", item.GetProperty("title").GetString());
        Assert.Equal(
            ["category", "id", "organization", "published_at", "region", "summary", "title", "url"],
            item.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(["id", "name"], item.GetProperty("organization").EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(["data", "next_cursor"], page.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Given_an_article_is_published_between_two_pages_When_the_second_page_is_read_Then_it_continues_where_the_first_ended()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await CollectAsync(factory, Toronto, "Redeemed Run Club", "toronto", TwentyFive());
        using var client = Client(factory, await KeyAsync(factory, "articles:read"));
        var before = await PageAsync(client, "limit=100");
        var first = await PageAsync(client, "limit=10");

        factory.Time.Advance(TimeSpan.FromMinutes(10));
        await CollectAsync(factory, "https://lifepointfit.ca/feed", "LifePoint Fitness", "toronto",
            new Item("Running clinic opens downtown", "https://lifepointfit.ca/n/1", Newest.AddHours(1)));
        var second = await PageAsync(client, $"limit=10&cursor={Uri.EscapeDataString(Cursor(first)!)}");

        Assert.Equal(
            before.GetProperty("data").EnumerateArray().Skip(10).Take(10).Select(Id),
            second.GetProperty("data").EnumerateArray().Select(Id));
    }

    [Fact]
    public async Task Given_an_article_whose_source_gave_no_time_When_listed_Then_it_sorts_by_when_Keryx_collected_it_and_its_published_at_is_null()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        // The run collects on 2026-11-27, after apple's time and before cedar's (2026-12-06).
        await CollectAsync(factory, Toronto, "Redeemed Run Club", "toronto",
            new Item("Running apple", "https://redeemedrun.ca/n/apple", Newest),
            new Item("Running birch", "https://redeemedrun.ca/n/birch"),
            new Item("Running cedar", "https://redeemedrun.ca/n/cedar", Newest.AddDays(10)));
        using var client = Client(factory, await KeyAsync(factory, "articles:read"));

        var page = await PageAsync(client, string.Empty);

        Assert.Equal(["Running cedar", "Running birch", "Running apple"], page.GetProperty("data").EnumerateArray().Select(item => item.GetProperty("title").GetString()));
        Assert.Equal(JsonValueKind.Null, page.GetProperty("data")[1].GetProperty("published_at").ValueKind);
    }

    private static string? Id(JsonElement item) => item.GetProperty("id").GetString();

    private static string? Cursor(JsonElement page) => page.GetProperty("next_cursor").GetString();

    private static async Task<JsonElement> PageAsync(HttpClient client, string query)
    {
        using var response = await client.GetAsync(new Uri($"/v1/articles?{query}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }
}
