using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using static Keryx.Api.Tests.Consumer.ConsumerApi;

namespace Keryx.Api.Tests.Consumer;

// Acceptance Test
// Traces to: L2-027 AC1-AC3; L2-015 AC3
// Description: A product reads one published article by its id. An article that is held, hidden
// or unknown is the same 404, so a product cannot tell them apart.
public sealed class ReadArticleTests
{
    private const string Toronto = "https://redeemedrun.ca/feed";
    private const string Global = "https://worldfaithfit.org/feed";
    private static readonly DateTimeOffset PublishedAt = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    // Default settings, T = 0.60. In Toronto one topic scores 0.60 (published) and none 0.30
    // (hidden); in the global region two topics score 0.48 (held for review).
    private static readonly Item Published = new("Sunrise running group meets weekly", "https://redeemedrun.ca/n/1", PublishedAt);
    private static readonly Item Clinic = new("Sunrise running clinic in High Park", "https://redeemedrun.ca/n/2", PublishedAt, EventStart: "2026-12-05T09:00:00-05:00", EventPlace: "High Park");
    private static readonly Item Hidden = new("Community bake sale this Saturday", "https://redeemedrun.ca/n/3", PublishedAt);
    private static readonly Item Held = new("Running and faith retreat", "https://worldfaithfit.org/1", PublishedAt);

    [Fact]
    public async Task Given_a_published_article_When_it_is_read_Then_200_with_the_list_fields_tags_collection_time_event_and_sources()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        var collectedAt = factory.Time.GetUtcNow();
        await CollectAsync(factory, Toronto, "Redeemed Run Club", "toronto", Published);
        using var client = Client(factory, await KeyAsync(factory, "articles:read"));
        var id = await ArticleIdAsync(factory, Published.Link);

        var article = await client.GetFromJsonAsync<JsonElement>(new Uri($"/v1/articles/{id}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(
            ["category", "collected_at", "event_id", "id", "organization", "published_at", "region", "sources", "summary", "tags", "title", "url"],
            article.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(id, article.GetProperty("id").GetString());
        Assert.Equal(Published.Title, article.GetProperty("title").GetString());
        var organization = article.GetProperty("organization");
        Assert.StartsWith("org_", organization.GetProperty("id").GetString(), StringComparison.Ordinal);
        Assert.Equal("Redeemed Run Club", organization.GetProperty("name").GetString());
        Assert.Equal("faith-wellness", article.GetProperty("category").GetString());
        Assert.Equal("toronto", article.GetProperty("region").GetString());
        Assert.Equal(PublishedAt, article.GetProperty("published_at").GetDateTimeOffset());
        Assert.Equal(Published.Link, article.GetProperty("url").GetString());
        Assert.Equal("All welcome.", article.GetProperty("summary").GetString());
        Assert.Equal(JsonValueKind.Array, article.GetProperty("tags").ValueKind);
        Assert.True(article.GetProperty("collected_at").GetDateTimeOffset() >= collectedAt);
        Assert.Equal(JsonValueKind.Null, article.GetProperty("event_id").ValueKind);
        var source = Assert.Single(article.GetProperty("sources").EnumerateArray());
        Assert.Equal(("Redeemed Run Club", Published.Link), (source.GetProperty("name").GetString(), source.GetProperty("url").GetString()));
    }

    [Fact]
    public async Task Given_an_article_of_an_event_When_it_is_read_Then_it_names_the_event()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await CollectAsync(factory, Toronto, "Redeemed Run Club", "toronto", Clinic);
        using var client = Client(factory, await KeyAsync(factory, "articles:read"));

        var article = await client.GetFromJsonAsync<JsonElement>(new Uri($"/v1/articles/{await ArticleIdAsync(factory, Clinic.Link)}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.StartsWith("evt_", article.GetProperty("event_id").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_held_hidden_unknown_and_malformed_ids_When_each_is_read_Then_each_is_the_same_404_article_not_found()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await CollectAsync(factory, Toronto, "Redeemed Run Club", "toronto", Hidden);
        await CollectAsync(factory, Global, "World Faith Fit", "global", Held);
        using var client = Client(factory, await KeyAsync(factory, "articles:read"));
        var ids = new[] { await ArticleIdAsync(factory, Hidden.Link), await ArticleIdAsync(factory, Held.Link), "art_none0000000000000000", "not-an-id", "evt_none0000000000000000" };

        var bodies = new List<string>();
        foreach (var id in ids)
        {
            using var response = await client.GetAsync(new Uri($"/v1/articles/{id}", UriKind.Relative), TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>(TestContext.Current.CancellationToken);
            Assert.Equal("article_not_found", body!["code"].GetString());
            body.Remove("trace_id");
            bodies.Add(JsonSerializer.Serialize(body.OrderBy(entry => entry.Key, StringComparer.Ordinal)));
        }

        Assert.Single(bodies.Distinct(StringComparer.Ordinal));
    }
}
