using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using static Keryx.Api.Tests.Consumer.ConsumerApi;

namespace Keryx.Api.Tests.Consumer;

// Acceptance Test
// Traces to: L2-026 AC3, AC4
// Description: A product filters the list by region, category, organization and time, alone or
// together; a filter it cannot use, or a cursor Keryx did not issue, is refused with its code.
public sealed class FilterArticlesTests
{
    private static readonly DateTimeOffset Day = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    // Each title names "running" (one topic): every item is published. "race" makes a race.
    private static readonly Item TorontoRun = new("Running apple", "https://redeemedrun.ca/n/apple", Day.AddDays(-2));
    private static readonly Item TorontoRace = new("Running birch 5K race", "https://redeemedrun.ca/n/birch", Day);
    private static readonly Item ScarboroughRun = new("Running cedar", "https://lifepointfit.ca/n/cedar", Day.AddDays(-1));
    private static readonly Item ScarboroughRace = new("Running delta 10K race", "https://lifepointfit.ca/n/delta", Day.AddDays(-3));

    public static TheoryData<string, string[]> Filters => new()
    {
        { "region=toronto", ["Running birch 5K race", "Running apple"] },
        { "category=races-challenges", ["Running birch 5K race", "Running delta 10K race"] },
        { "since=2026-11-25T12:00:00Z", ["Running birch 5K race", "Running cedar"] },
        { "since=2026-11-25T07:00:00-05:00", ["Running birch 5K race", "Running cedar"] },
        { "region=mississauga&category=races-challenges", ["Running delta 10K race"] },
        { "region=toronto&category=races-challenges&since=2026-11-26T12:00:00%2B00:00", ["Running birch 5K race"] },
        { "region=gta", [] },
        // A parameter the endpoint does not define changes nothing (L2-025).
        { "colour=blue", ["Running birch 5K race", "Running cedar", "Running apple", "Running delta 10K race"] },
    };

    [Theory]
    [MemberData(nameof(Filters))]
    public async Task Given_articles_across_regions_categories_and_times_When_filters_are_sent_Then_only_those_matching_every_filter_return(string query, string[] titles)
    {
        using var factory = new ApiFactory();
        using var client = await SeededAsync(factory);

        var page = await PageAsync(client, query);

        Assert.Equal(titles, Titles(page));
    }

    [Fact]
    public async Task Given_an_organization_id_When_it_filters_Then_only_its_articles_return_and_an_unknown_one_returns_none()
    {
        using var factory = new ApiFactory();
        using var client = await SeededAsync(factory);
        var all = await PageAsync(client, string.Empty);
        var lifePoint = all.GetProperty("data").EnumerateArray()
            .Single(item => item.GetProperty("title").GetString() == ScarboroughRun.Title).GetProperty("organization").GetProperty("id").GetString();

        var mine = await PageAsync(client, $"org={lifePoint}&category=races-challenges");
        var unknown = await PageAsync(client, "org=org_aaaaaaaaaaaa");

        Assert.Equal(["Running delta 10K race"], Titles(mine));
        Assert.Empty(Titles(unknown));
    }

    [Theory]
    [InlineData("region=atlantis", "invalid_filter")]
    [InlineData("region=Toronto", "invalid_filter")]
    [InlineData("category=yoga", "invalid_filter")]
    [InlineData("since=yesterday", "invalid_filter")]
    [InlineData("since=2026-11-26T12:00:00", "invalid_filter")]
    [InlineData("since=2026-11-26", "invalid_filter")]
    [InlineData("org=lifepoint", "invalid_filter")]
    [InlineData("org=org_AAAAAAAAAAAA", "invalid_filter")]
    [InlineData("org=src_aaaaaaaaaaaa", "invalid_filter")]
    [InlineData("cursor=abc", "invalid_cursor")]
    public async Task Given_a_filter_it_cannot_use_or_a_cursor_Keryx_did_not_issue_When_listed_Then_400_with_its_code(string query, string code)
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        using var client = Client(factory, await KeyAsync(factory, "articles:read"));

        using var response = await client.GetAsync(new Uri($"/v1/articles?{query}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(code, problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Given_a_cursor_When_it_is_altered_or_sent_with_other_filters_Then_400_invalid_cursor()
    {
        using var factory = new ApiFactory();
        using var client = await SeededAsync(factory);
        var cursor = (await PageAsync(client, "limit=1&region=toronto")).GetProperty("next_cursor").GetString()!;
        var altered = cursor[..^2] + (cursor[^2] == 'A' ? "B" : "A") + cursor[^1];

        foreach (var query in new[] { $"limit=1&region=toronto&cursor={altered}", $"limit=1&cursor={cursor}", $"limit=1&region=mississauga&cursor={cursor}" })
        {
            using var response = await client.GetAsync(new Uri($"/v1/articles?{query}", UriKind.Relative), TestContext.Current.CancellationToken);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
            Assert.Equal((HttpStatusCode.BadRequest, "invalid_cursor"), (response.StatusCode, problem.GetProperty("code").GetString()));
        }

        // With its own filters it reads on, and a different limit is allowed.
        Assert.Equal(["Running apple"], Titles(await PageAsync(client, $"limit=5&region=toronto&cursor={cursor}")));
    }

    private static async Task<HttpClient> SeededAsync(ApiFactory factory)
    {
        await StaffClient.SeedAsync(factory);
        await CollectAsync(factory, "https://redeemedrun.ca/feed", "Redeemed Run Club", "toronto", TorontoRun, TorontoRace);
        await CollectAsync(factory, "https://lifepointfit.ca/feed", "LifePoint Fitness", "mississauga", ScarboroughRun, ScarboroughRace);
        return Client(factory, await KeyAsync(factory, "articles:read"));
    }

    private static IEnumerable<string?> Titles(JsonElement page) =>
        page.GetProperty("data").EnumerateArray().Select(item => item.GetProperty("title").GetString());

    private static async Task<JsonElement> PageAsync(HttpClient client, string query)
    {
        using var response = await client.GetAsync(new Uri($"/v1/articles?{query}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }
}
