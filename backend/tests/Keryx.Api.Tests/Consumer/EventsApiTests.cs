using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using static Keryx.Api.Tests.Consumer.ConsumerApi;

namespace Keryx.Api.Tests.Consumer;

// Acceptance Test
// Traces to: L2-028 AC1-AC5
// Description: A product lists upcoming served events soonest first, a page at a time, filtered by
// time range, region and organization, and reads one by id.
public sealed class EventsApiTests
{
    // The fixture's clock reads 2026-11-27 15:00 UTC. Every title names "running" (one topic), so
    // its article is published in a focus area; the bake sale names none and is held.
    private static readonly Item Apple = Event("Running apple", "https://redeemedrun.ca/n/apple", "2026-12-01T09:00:00-05:00", "High Park");
    private static readonly Item Birch = Event("Running birch", "https://redeemedrun.ca/n/birch", "2026-12-03T09:00:00-05:00", "Christie Pits");
    private static readonly Item Cedar = Event("Running cedar", "https://redeemedrun.ca/n/cedar", "2026-12-03T09:00:00-05:00", "Trinity Bellwoods");
    private static readonly Item Delta = Event("Running delta", "https://redeemedrun.ca/n/delta", "2026-11-20T09:00:00-05:00", "High Park");
    private static readonly Item BakeSale = Event("Community bake sale", "https://redeemedrun.ca/n/bake", "2026-12-02T10:00:00-05:00", "Church hall");
    private static readonly Item Ember = Event("Running ember", "https://lifepointfit.ca/n/ember", "2026-12-02T18:00:00-05:00", "Port Credit");
    private static readonly Item Fjord = Event("Running fjord", "https://lifepointfit.ca/n/fjord", "2027-06-01T08:00:00-04:00", "Port Credit");

    [Fact]
    public async Task Given_upcoming_past_and_held_events_When_listed_2_at_a_time_Then_only_upcoming_served_ones_come_soonest_first_each_once()
    {
        using var factory = new ApiFactory();
        using var client = await SeededAsync(factory);

        var first = await PageAsync(client, "limit=2");
        var second = await PageAsync(client, $"limit=2&cursor={Uri.EscapeDataString(Cursor(first)!)}");
        var third = await PageAsync(client, $"limit=2&cursor={Uri.EscapeDataString(Cursor(second)!)}");

        Assert.Equal(["Running apple", "Running ember"], Titles(first));
        Assert.Equal(["Running birch", "Running cedar"], Titles(second));
        Assert.Equal(["Running fjord"], Titles(third));
        Assert.Equal(JsonValueKind.Null, third.GetProperty("next_cursor").ValueKind);
    }

    [Fact]
    public async Task Given_an_event_When_it_is_listed_and_read_by_id_Then_it_has_exactly_the_event_fields_with_its_start_in_UTC()
    {
        using var factory = new ApiFactory();
        using var client = await SeededAsync(factory);
        var id = await EventIdAsync(factory, Apple.Link);

        var listed = (await PageAsync(client, "limit=1")).GetProperty("data")[0];
        using var response = await client.GetAsync(new Uri($"/v1/events/{id}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var read = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(listed.GetRawText(), read.GetRawText());
        Assert.Equal(
            ["article_id", "ends_at", "id", "organization", "place_name", "region", "starts_at", "time_zone", "title", "url"],
            read.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(id, read.GetProperty("id").GetString());
        Assert.StartsWith("evt_", id, StringComparison.Ordinal);
        var starts = read.GetProperty("starts_at").GetDateTimeOffset();
        Assert.Equal((new DateTimeOffset(2026, 12, 1, 14, 0, 0, TimeSpan.Zero), TimeSpan.Zero), (starts, starts.Offset));
        Assert.Equal(JsonValueKind.Null, read.GetProperty("ends_at").ValueKind);
        Assert.Equal(
            ("America/Toronto", "High Park", "toronto", "Redeemed Run Club", Apple.Link, await ArticleIdAsync(factory, Apple.Link)),
            (read.GetProperty("time_zone").GetString(), read.GetProperty("place_name").GetString(), read.GetProperty("region").GetString(),
             read.GetProperty("organization").GetProperty("name").GetString(), read.GetProperty("url").GetString(), read.GetProperty("article_id").GetString()));
    }

    public static TheoryData<string, string[]> Filters => new()
    {
        { "from=2026-12-02T00:00:00Z&to=2026-12-03T23:59:59Z", ["Running ember", "Running birch", "Running cedar"] },
        { "to=2026-12-02T00:00:00Z", ["Running apple"] },
        // Both ends are inclusive: an event starting exactly at to is in the range.
        { "from=2026-12-01T14:00:00Z&to=2026-12-01T09:00:00-05:00", ["Running apple"] },
        // A from earlier than now still starts from now: past events are not listed.
        { "from=2026-01-01T00:00:00Z", ["Running apple", "Running ember", "Running birch", "Running cedar", "Running fjord"] },
        // Exactly 366 days is allowed.
        { "from=2026-12-01T00:00:00Z&to=2027-12-02T00:00:00Z", ["Running apple", "Running ember", "Running birch", "Running cedar", "Running fjord"] },
        { "region=mississauga", ["Running ember", "Running fjord"] },
        { "region=mississauga&to=2027-01-01T00:00:00-05:00", ["Running ember"] },
        { "to=2026-11-01T00:00:00Z", [] },
    };

    [Theory]
    [MemberData(nameof(Filters))]
    public async Task Given_a_range_or_region_When_it_filters_Then_only_events_starting_inside_it_return(string query, string[] titles)
    {
        using var factory = new ApiFactory();
        using var client = await SeededAsync(factory);

        Assert.Equal(titles, Titles(await PageAsync(client, query)));
    }

    [Fact]
    public async Task Given_an_organization_id_When_it_filters_Then_only_its_events_return()
    {
        using var factory = new ApiFactory();
        using var client = await SeededAsync(factory);
        var lifePoint = (await PageAsync(client, "region=mississauga")).GetProperty("data")[0].GetProperty("organization").GetProperty("id").GetString();

        Assert.Equal(["Running ember", "Running fjord"], Titles(await PageAsync(client, $"org={lifePoint}")));
    }

    [Theory]
    [InlineData("from=2026-12-03T00:00:00Z&to=2026-12-02T00:00:00Z", "invalid_filter")]
    [InlineData("from=2026-12-01T00:00:00Z&to=2027-12-03T00:00:00Z", "invalid_filter")]
    [InlineData("to=2027-11-29T00:00:00Z", "invalid_filter")]
    [InlineData("from=tomorrow", "invalid_filter")]
    [InlineData("to=2026-12-02", "invalid_filter")]
    [InlineData("region=atlantis", "invalid_filter")]
    [InlineData("org=lifepoint", "invalid_filter")]
    [InlineData("limit=0", "invalid_limit")]
    [InlineData("limit=101", "invalid_limit")]
    [InlineData("cursor=abc", "invalid_cursor")]
    public async Task Given_a_value_it_cannot_use_When_events_are_listed_Then_400_with_its_code(string query, string code)
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        using var client = Client(factory, await KeyAsync(factory, "events:read"));

        using var response = await client.GetAsync(new Uri($"/v1/events?{query}", UriKind.Relative), TestContext.Current.CancellationToken);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal((HttpStatusCode.BadRequest, code), (response.StatusCode, problem.GetProperty("code").GetString()));
    }

    [Fact]
    public async Task Given_a_cursor_When_it_is_sent_with_other_filters_Then_400_invalid_cursor()
    {
        using var factory = new ApiFactory();
        using var client = await SeededAsync(factory);
        var cursor = Uri.EscapeDataString(Cursor(await PageAsync(client, "limit=1"))!);

        using var response = await client.GetAsync(new Uri($"/v1/events?limit=1&region=toronto&cursor={cursor}", UriKind.Relative), TestContext.Current.CancellationToken);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal((HttpStatusCode.BadRequest, "invalid_cursor"), (response.StatusCode, problem.GetProperty("code").GetString()));
    }

    [Fact]
    public async Task Given_a_past_served_event_When_it_is_read_by_id_Then_200()
    {
        using var factory = new ApiFactory();
        using var client = await SeededAsync(factory);

        using var response = await client.GetAsync(new Uri($"/v1/events/{await EventIdAsync(factory, Delta.Link)}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("held")]
    [InlineData("evt_aaaaaaaaaaaa")]
    [InlineData("nonsense")]
    public async Task Given_an_event_that_is_not_served_or_does_not_exist_When_it_is_read_Then_404_event_not_found(string which)
    {
        using var factory = new ApiFactory();
        using var client = await SeededAsync(factory);
        var id = which == "held" ? await EventIdAsync(factory, BakeSale.Link) : which;

        using var response = await client.GetAsync(new Uri($"/v1/events/{id}", UriKind.Relative), TestContext.Current.CancellationToken);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal((HttpStatusCode.NotFound, "event_not_found"), (response.StatusCode, problem.GetProperty("code").GetString()));
    }

    [Theory]
    [InlineData("/v1/events")]
    [InlineData("/v1/events/evt_aaaaaaaaaaaa")]
    public async Task Given_a_key_without_events_read_When_events_are_requested_Then_403_insufficient_scope(string path)
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        using var client = Client(factory, await KeyAsync(factory, "articles:read"));

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal((HttpStatusCode.Forbidden, "insufficient_scope"), (response.StatusCode, problem.GetProperty("code").GetString()));
    }

    private static Item Event(string title, string link, string start, string place) =>
        new(title, link, new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero), EventStart: start, EventPlace: place);

    private static async Task<HttpClient> SeededAsync(ApiFactory factory)
    {
        await StaffClient.SeedAsync(factory);
        await CollectAsync(factory, "https://redeemedrun.ca/feed", "Redeemed Run Club", "toronto", Apple, Birch, Cedar, Delta, BakeSale);
        await CollectAsync(factory, "https://lifepointfit.ca/feed", "LifePoint Fitness", "mississauga", Ember, Fjord);
        return Client(factory, await KeyAsync(factory, "events:read"));
    }

    private static IEnumerable<string?> Titles(JsonElement page) =>
        page.GetProperty("data").EnumerateArray().Select(item => item.GetProperty("title").GetString());

    private static string? Cursor(JsonElement page) => page.GetProperty("next_cursor").GetString();

    private static async Task<JsonElement> PageAsync(HttpClient client, string query)
    {
        using var response = await client.GetAsync(new Uri($"/v1/events?{query}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }
}
