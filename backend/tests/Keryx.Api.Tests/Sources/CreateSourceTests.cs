using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;

namespace Keryx.Api.Tests.Sources;

// Acceptance Test
// Traces to: L2-001
// Description: Staff add a source to the catalogue through the admin API.
public sealed class CreateSourceTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly Uri Sources = new("/admin/v1/sources", UriKind.Relative);

    // L2-001 AC1.
    [Fact]
    public async Task Given_a_valid_rss_source_When_it_is_created_Then_it_is_201_and_reads_back_the_same()
    {
        await StaffClient.SeedAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);

        using var created = await client.PostAsJsonAsync(Sources, new
        {
            kind = "rss",
            address = "https://lifepointfit.ca/events/feed",
            organization_name = "LifePoint Fitness",
            region = "toronto",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var id = body.GetProperty("id").GetString();
        Assert.Matches("^src_[a-z2-7]{12}$", id);
        Assert.Equal($"/admin/v1/sources/{id}", created.Headers.Location?.OriginalString);

        var read = await client.GetFromJsonAsync<JsonElement>(created.Headers.Location, TestContext.Current.CancellationToken);
        Assert.Equal(
            ["id", "kind", "address", "organization_name", "region", "health", "failure_reason", "consecutive_failures", "article_count", "created_at", "last_fetched_at", "last_succeeded_at"],
            read.EnumerateObject().Select(property => property.Name));
        Assert.Equal(id, read.GetProperty("id").GetString());
        Assert.Equal("rss", read.GetProperty("kind").GetString());
        Assert.Equal("https://lifepointfit.ca/events/feed", read.GetProperty("address").GetString());
        Assert.Equal("LifePoint Fitness", read.GetProperty("organization_name").GetString());
        Assert.Equal("toronto", read.GetProperty("region").GetString());
        Assert.Equal("healthy", read.GetProperty("health").GetString());
        Assert.Equal(factory.Time.GetUtcNow(), read.GetProperty("created_at").GetDateTimeOffset());
        Assert.Equal(JsonValueKind.Null, read.GetProperty("last_fetched_at").ValueKind);
        Assert.Equal(JsonValueKind.Null, read.GetProperty("last_succeeded_at").ValueKind);
        Assert.Equal(body.GetRawText(), read.GetRawText());
    }

    [Theory]
    [InlineData("src_aaaaaaaaaaaa")]
    [InlineData("not-an-id")]
    public async Task Given_no_source_with_the_id_When_it_is_read_Then_it_is_404_source_not_found(string id)
    {
        using var client = await StaffClient.OpenAsync(factory);

        using var response = await client.GetAsync(new Uri($"/admin/v1/sources/{id}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("source_not_found", problem.GetProperty("code").GetString());
    }
}
