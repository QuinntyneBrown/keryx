using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using Keryx.Domain.Sources;
using static Keryx.Api.Tests.Runs.CollectionRuns;

namespace Keryx.Api.Tests.Sources;

// Acceptance Test
// Traces to: L2-003 AC1, AC2, AC5
// Description: Staff change a source's address, organization and region. The change is saved,
// the saved address is test-fetched once and the result returned, and the next run fetches the
// new address; a change that would equal another source, or that breaks a rule, changes nothing.
public sealed class EditSourceTests
{
    private const string Old = "https://redeemedrun.ca/feed";
    private const string New = "https://redeemedrun.ca/events/feed";

    [Fact]
    public async Task Given_a_failing_source_When_staff_save_a_new_address_organization_and_region_Then_200_with_the_saved_source_and_one_test_of_the_new_address()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        var id = await AddSourceAsync(factory, SourceKind.Rss, Old, "Redeemed Run Club", "toronto");
        await RunAsync(factory);
        factory.Sources.AnswerFeed(New, Feed(("Sunrise running club at St. Paul's church", "https://redeemedrun.ca/1", "Sunrise running club at St. Paul's church")));
        var fetched = factory.Sources.Requests.Count;

        using var response = await EditAsync(client, id, New, "Redeemed Run Club Toronto", "global");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(["source", "test"], body.EnumerateObject().Select(property => property.Name));
        var source = body.GetProperty("source");
        Assert.Equal((id, "rss", New, "Redeemed Run Club Toronto", "global"), (
            source.GetProperty("id").GetString(),
            source.GetProperty("kind").GetString(),
            source.GetProperty("address").GetString(),
            source.GetProperty("organization_name").GetString(),
            source.GetProperty("region").GetString()));
        // The test does not change the stored health: the next run does (L2-002).
        Assert.Equal("failing", source.GetProperty("health").GetString());
        var test = body.GetProperty("test");
        Assert.True(test.GetProperty("reachable").GetBoolean());
        Assert.Equal(1, test.GetProperty("items_found").GetInt32());
        Assert.Equal([New], factory.Sources.Requests.Skip(fetched).Select(request => request.Address.OriginalString));
        var read = await client.GetFromJsonAsync<JsonElement>(new Uri($"/admin/v1/sources/{id}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(source.GetRawText(), read.GetRawText());

        // The next run fetches the new address, not the old.
        fetched = factory.Sources.Requests.Count;
        await RunAsync(factory);
        Assert.Equal([New], factory.Sources.Requests.Skip(fetched).Select(request => request.Address.OriginalString));
        // The old address is free for a source again; the new one is taken.
        using var old = await client.PostAsJsonAsync(new Uri("/admin/v1/sources", UriKind.Relative), new { kind = "rss", address = Old, organization_name = "Redeemed", region = "toronto" }, TestContext.Current.CancellationToken);
        using var taken = await client.PostAsJsonAsync(new Uri("/admin/v1/sources", UriKind.Relative), new { kind = "rss", address = New + "/", organization_name = "Redeemed", region = "toronto" }, TestContext.Current.CancellationToken);
        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.Conflict), (old.StatusCode, taken.StatusCode));
    }

    [Fact]
    public async Task Given_a_source_When_staff_change_only_its_organization_Then_it_is_saved_and_its_own_address_is_no_collision()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        var id = await AddSourceAsync(factory, SourceKind.Rss, Old, "Redeemed Run Club", "toronto");

        using var response = await EditAsync(client, id, "https://RedeemedRun.ca/feed/", "Redeemed Run Club Toronto", "toronto");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("Redeemed Run Club Toronto", body.GetProperty("source").GetProperty("organization_name").GetString());
        Assert.False(body.GetProperty("test").GetProperty("reachable").GetBoolean());
    }

    [Fact]
    public async Task Given_another_source_with_the_address_When_staff_save_it_Then_409_source_exists_and_nothing_changes()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        var id = await AddSourceAsync(factory, SourceKind.Rss, Old, "Redeemed Run Club", "toronto");
        await AddSourceAsync(factory, SourceKind.Rss, New, "Redeemed Events", "toronto");
        // Another kind at the address is a different source.
        await AddSourceAsync(factory, SourceKind.Website, "https://templefit.ca/", "Temple Fit", "toronto");
        var fetched = factory.Sources.Requests.Count;

        using var refused = await EditAsync(client, id, "https://REDEEMEDRUN.ca/events/feed/", "Renamed", "global");
        using var website = await EditAsync(client, id, "https://templefit.ca/", "Redeemed Run Club", "toronto");

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        var problem = await refused.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("source_exists", problem.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, website.StatusCode);
        Assert.Equal(["https://templefit.ca/"], factory.Sources.Requests.Skip(fetched).Select(request => request.Address.OriginalString));
        var read = await client.GetFromJsonAsync<JsonElement>(new Uri($"/admin/v1/sources/{id}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal("Redeemed Run Club", read.GetProperty("organization_name").GetString());
    }

    // L2-003 AC1 with the rules of L2-001 AC2: one row per rule, naming the one field that fails.
    [Theory]
    [InlineData("address", "redeemedrun.ca/feed", "Redeemed Run Club", "toronto")]
    [InlineData("address", "https://user:secret@redeemedrun.ca/feed", "Redeemed Run Club", "toronto")]
    [InlineData("address", null, "Redeemed Run Club", "toronto")]
    [InlineData("organization_name", Old, " ", "toronto")]
    [InlineData("organization_name", Old, null, "toronto")]
    [InlineData("region", Old, "Redeemed Run Club", "atlantis")]
    public async Task Given_one_invalid_field_When_staff_save_Then_400_naming_it_and_nothing_changes(string field, string? address, string? organization, string? region)
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        var id = await AddSourceAsync(factory, SourceKind.Rss, Old, "Redeemed Run Club", "toronto");

        using var response = await EditAsync(client, id, address, organization, region);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
        Assert.Equal([field], problem.GetProperty("errors").EnumerateObject().Select(entry => entry.Name));
        Assert.Empty(factory.Sources.Requests);
    }

    [Fact]
    public async Task Given_a_news_query_When_staff_change_its_query_text_Then_it_is_saved()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        var id = await AddSourceAsync(factory, SourceKind.NewsQuery, "christian fitness toronto", "Toronto news", "toronto");

        using var response = await EditAsync(client, id, "christian running toronto", "Toronto news", "toronto");
        using var empty = await EditAsync(client, id, " ", "Toronto news", "toronto");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("christian running toronto", body.GetProperty("source").GetProperty("address").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Empty(factory.Sources.Requests);
    }

    // L2-003 AC5.
    [Theory]
    [InlineData("src_aaaaaaaaaaaa")]
    [InlineData("not-an-id")]
    public async Task Given_no_source_with_the_id_When_staff_save_it_Then_404_source_not_found(string id)
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);

        using var response = await EditAsync(client, id, Old, "Redeemed Run Club", "toronto");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("source_not_found", problem.GetProperty("code").GetString());
    }

    internal static Task<HttpResponseMessage> EditAsync(HttpClient client, string id, string? address, string? organization, string? region) =>
        client.PutAsJsonAsync(new Uri($"/admin/v1/sources/{id}", UriKind.Relative), new { address, organization_name = organization, region }, TestContext.Current.CancellationToken);

    internal static async Task<HttpClient> OpenAsync(ApiFactory factory)
    {
        await StaffClient.SeedAsync(factory);
        return await StaffClient.OpenAsync(factory);
    }
}
