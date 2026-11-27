using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using Keryx.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Keryx.Api.Tests.Sources;

// Acceptance Test
// Traces to: L2-001, L2-083
// Description: The admin API refuses a source it cannot store, and stores nothing.
public sealed class CreateSourceRefusalTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly Uri Sources = new("/admin/v1/sources", UriKind.Relative);

    // L2-001 AC2 and L2-083 AC1: one row per rule; each names the one field that fails.
    [Theory]
    [InlineData("kind", "rss2", "https://lifepointfit.ca/feed", "LifePoint Fitness", "toronto")]
    [InlineData("kind", null, "https://lifepointfit.ca/feed", "LifePoint Fitness", "toronto")]
    [InlineData("address", "rss", "lifepointfit.ca/feed", "LifePoint Fitness", "toronto")]
    [InlineData("address", "rss", "ftp://lifepointfit.ca/feed", "LifePoint Fitness", "toronto")]
    [InlineData("address", "rss", "javascript:alert(1)", "LifePoint Fitness", "toronto")]
    [InlineData("address", "rss", "https://user:secret@lifepointfit.ca/feed", "LifePoint Fitness", "toronto")]
    [InlineData("address", "rss", "https://lifepointfit.ca:8443/feed", "LifePoint Fitness", "toronto")]
    [InlineData("address", "rss", null, "LifePoint Fitness", "toronto")]
    [InlineData("organization_name", "rss", "https://lifepointfit.ca/feed", "", "toronto")]
    [InlineData("organization_name", "rss", "https://lifepointfit.ca/feed", "   ", "toronto")]
    [InlineData("organization_name", "rss", "https://lifepointfit.ca/feed", null, "toronto")]
    [InlineData("region", "rss", "https://lifepointfit.ca/feed", "LifePoint Fitness", "atlantis")]
    [InlineData("region", "rss", "https://lifepointfit.ca/feed", "LifePoint Fitness", null)]
    public async Task Given_one_invalid_field_When_a_source_is_created_Then_it_is_400_naming_that_field_and_nothing_is_stored(
        string field, string? kind, string? address, string? organization, string? region)
    {
        await StaffClient.SeedAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);
        var before = await CountAsync();

        using var response = await client.PostAsJsonAsync(Sources, new
        {
            kind,
            address,
            organization_name = organization,
            region,
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
        Assert.Equal([field], problem.GetProperty("errors").EnumerateObject().Select(entry => entry.Name));
        Assert.Equal(before, await CountAsync());
    }

    [Fact]
    public async Task Given_an_organization_name_of_120_characters_When_a_source_is_created_Then_it_is_stored_and_121_is_not()
    {
        await StaffClient.SeedAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);

        using var longest = await client.PostAsJsonAsync(Sources, Body("https://lifepointfit.ca/a", new string('a', 120)), TestContext.Current.CancellationToken);
        using var tooLong = await client.PostAsJsonAsync(Sources, Body("https://lifepointfit.ca/b", new string('a', 121)), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, longest.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }

    // L2-001 AC5: news query sources come from seed data only.
    [Fact]
    public async Task Given_kind_news_query_When_a_source_is_created_Then_it_is_400_kind_not_creatable()
    {
        await StaffClient.SeedAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);
        var before = await CountAsync();

        using var response = await client.PostAsJsonAsync(Sources, new
        {
            kind = "news_query",
            address = "https://news.example/search?q=fitness",
            organization_name = "Toronto news",
            region = "toronto",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("kind_not_creatable", problem.GetProperty("code").GetString());
        Assert.Equal(before, await CountAsync());
    }

    // L2-083 AC2: an admin body over 64 KB is refused before it is read.
    [Fact]
    public async Task Given_a_body_over_64_KB_When_it_is_sent_Then_it_is_413()
    {
        using var client = await StaffClient.OpenAsync(factory);
        var padding = new string('a', (64 * 1024) + 1 - "{\"organization_name\":\"\"}".Length);
        using var content = new StringContent($"{{\"organization_name\":\"{padding}\"}}", Encoding.UTF8, "application/json");
        Assert.Equal((64 * 1024) + 1, Encoding.UTF8.GetByteCount(await content.ReadAsStringAsync(TestContext.Current.CancellationToken)));

        using var response = await client.PostAsync(Sources, content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    // L2-083 AC5: a property the endpoint does not define cannot set a field it does not expose.
    [Fact]
    public async Task Given_extra_properties_When_a_source_is_created_Then_they_are_ignored()
    {
        await StaffClient.SeedAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);

        using var response = await client.PostAsJsonAsync(Sources, new
        {
            kind = "rss",
            address = "https://lifepointfit.ca/over-posted",
            organization_name = "LifePoint Fitness",
            region = "toronto",
            id = "src_chosenbyme12",
            health = "failing",
            created_at = "2001-01-01T00:00:00Z",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.NotEqual("src_chosenbyme12", body.GetProperty("id").GetString());
        Assert.Equal("healthy", body.GetProperty("health").GetString());
        Assert.Equal(factory.Time.GetUtcNow(), body.GetProperty("created_at").GetDateTimeOffset());
    }

    private static object Body(string address, string organization) =>
        new { kind = "rss", address, organization_name = organization, region = "toronto" };

    private async Task<int> CountAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAppDbContext>().Sources.CountAsync(TestContext.Current.CancellationToken);
    }
}
