using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using static Keryx.Api.Tests.Consumer.ConsumerApi;

namespace Keryx.Api.Tests.Catalogue;

// Acceptance Test
// Traces to: L2-014 AC1, AC5, AC6
// Description: Staff publish or hide any article whatever its score; the consumer API follows on the
// next request, the same decision again changes nothing, and an unknown id is article_not_found.
public sealed class DecideOnArticleTests
{
    private const string Toronto = "https://redeemedrun.ca/feed";
    private const string Global = "https://worldfaithfit.org/feed";

    // Default settings, T = 0.60: one topic in Toronto publishes (0.60), two topics abroad are held
    // (0.48), and no topic in Toronto is hidden (0.30).
    internal static readonly Item Published = new("Sunrise running group meets weekly", "https://redeemedrun.ca/n/1");
    internal static readonly Item Hidden = new("Community bake sale this Saturday", "https://redeemedrun.ca/n/2");
    internal static readonly Item Held = new("Running and faith retreat", "https://worldfaithfit.org/1");

    [Fact]
    public async Task Given_a_held_article_When_staff_publish_it_Then_200_published_by_staff_and_served_on_the_next_request()
    {
        using var factory = await CollectedAsync();
        using var staff = await StaffClient.OpenAsync(factory);
        using var product = Client(factory, await KeyAsync(factory, "articles:read"));
        var id = await ArticleIdAsync(factory, Held.Link);
        using (var before = await product.GetAsync(new Uri($"/v1/articles/{id}", UriKind.Relative), TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.NotFound, before.StatusCode);
        }

        using var response = await DecideAsync(staff, id, "publish");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(
            ["decided_by", "id", "previous_status", "status"],
            body.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal((id, "published", "staff", "needs_review"), Decision(body));
        var read = await staff.GetFromJsonAsync<JsonElement>(new Uri($"/admin/v1/articles/{id}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(("published", "staff"), (read.GetProperty("status").GetString(), read.GetProperty("decided_by").GetString()));
        Assert.Equal(JsonValueKind.Null, read.GetProperty("status_reason").ValueKind);
        using var served = await product.GetAsync(new Uri($"/v1/articles/{id}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
    }

    [Fact]
    public async Task Given_a_hidden_article_When_staff_publish_it_Then_it_is_published_with_hidden_as_its_previous_status()
    {
        using var factory = await CollectedAsync();
        using var staff = await StaffClient.OpenAsync(factory);
        var id = await ArticleIdAsync(factory, Hidden.Link);

        using var response = await DecideAsync(staff, id, "publish");

        Assert.Equal((id, "published", "staff", "hidden"), Decision(await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task Given_an_article_staff_published_When_it_is_published_again_Then_200_and_nothing_changes()
    {
        using var factory = await CollectedAsync();
        using var staff = await StaffClient.OpenAsync(factory);
        var id = await ArticleIdAsync(factory, Held.Link);
        using (await DecideAsync(staff, id, "publish"))
        {
        }

        using var again = await DecideAsync(staff, id, "publish");

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal((id, "published", "staff", "needs_review"), Decision(await again.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task Given_an_article_the_run_published_When_staff_publish_it_Then_200_and_it_stays_decided_by_the_system()
    {
        using var factory = await CollectedAsync();
        using var staff = await StaffClient.OpenAsync(factory);
        var id = await ArticleIdAsync(factory, Published.Link);

        using var response = await DecideAsync(staff, id, "publish");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal((id, "published", "system"), (body.GetProperty("id").GetString(), body.GetProperty("status").GetString(), body.GetProperty("decided_by").GetString()));
        Assert.Equal(JsonValueKind.Null, body.GetProperty("previous_status").ValueKind);
    }

    [Theory]
    [InlineData("publish")]
    public async Task Given_an_unknown_id_When_a_decision_is_sent_Then_404_article_not_found(string decision)
    {
        using var factory = await CollectedAsync();
        using var staff = await StaffClient.OpenAsync(factory);

        using var response = await DecideAsync(staff, "art_doesnotexist000000", decision);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("article_not_found", problem.GetProperty("code").GetString());
    }

    internal static async Task<ApiFactory> CollectedAsync()
    {
        var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await CollectAsync(factory, Toronto, "Redeemed Run Club", "toronto", Published, Hidden);
        await CollectAsync(factory, Global, "World Faith Fit", "global", Held);
        return factory;
    }

    internal static Task<HttpResponseMessage> DecideAsync(HttpClient staff, string id, string decision, object? body = null) =>
        staff.PostAsJsonAsync(new Uri($"/admin/v1/articles/{id}/{decision}", UriKind.Relative), body ?? new { }, TestContext.Current.CancellationToken);

    internal static (string?, string?, string?, string?) Decision(JsonElement body) => (
        body.GetProperty("id").GetString(),
        body.GetProperty("status").GetString(),
        body.GetProperty("decided_by").GetString(),
        body.GetProperty("previous_status").GetString());
}
