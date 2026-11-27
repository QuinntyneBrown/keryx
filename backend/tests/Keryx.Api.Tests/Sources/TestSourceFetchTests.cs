using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using Keryx.Application.Abstractions;
using Keryx.Application.Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Keryx.Api.Tests.Sources;

// Acceptance Test
// Traces to: L2-002, L2-024
// Description: Staff test-fetch an address, or a source in the catalogue, and Keryx stores nothing.
public sealed class TestSourceFetchTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly Uri TestAddress = new("/admin/v1/sources/test", UriKind.Relative);

    // Three items: two in Toronto about church fitness (0.80, published at the default 0.60),
    // one about a council budget (0.30, hidden).
    private const string Feed = """
        <?xml version="1.0" encoding="utf-8"?>
        <rss version="2.0"><channel><title>LifePoint</title>
          <item><title>Church fitness night</title><link>https://lifepointfit.ca/e/1</link><description>Toronto, ON</description></item>
          <item><title>Faith and running club</title><link>https://lifepointfit.ca/e/2</link><description>Toronto, ON</description></item>
          <item><title>Council budget</title><link>https://lifepointfit.ca/e/3</link><description>Toronto, ON</description></item>
        </channel></rss>
        """;

    // L2-002 AC1.
    [Fact]
    public async Task Given_a_reachable_feed_When_it_is_tested_Then_it_reports_items_found_and_relevant_as_numbers_and_stores_nothing()
    {
        await StaffClient.SeedAsync(factory);
        factory.Sources.AnswerFeed("https://lifepointfit.ca/test/feed", Feed);
        using var client = await StaffClient.OpenAsync(factory);
        var before = await CountAsync();

        using var response = await client.PostAsJsonAsync(TestAddress, new { kind = "rss", address = "https://lifepointfit.ca/test/feed" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(["reachable", "reason", "items_found", "items_relevant"], body.EnumerateObject().Select(property => property.Name));
        Assert.True(body.GetProperty("reachable").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("reason").ValueKind);
        Assert.Equal(JsonValueKind.Number, body.GetProperty("items_found").ValueKind);
        Assert.Equal(3, body.GetProperty("items_found").GetInt32());
        Assert.Equal(2, body.GetProperty("items_relevant").GetInt32());
        Assert.Equal(before, await CountAsync());
    }

    // L2-002 AC2: an unreachable address is still 200, with a reason from the fixed list.
    [Theory]
    [InlineData(FetchFailureReason.NotFound, "not_found")]
    [InlineData(FetchFailureReason.Timeout, "timeout")]
    [InlineData(FetchFailureReason.BlockedByRobots, "blocked_by_robots")]
    [InlineData(FetchFailureReason.Refused, "refused")]
    [InlineData(FetchFailureReason.InvalidContent, "invalid_content")]
    public async Task Given_a_fetch_that_fails_When_it_is_tested_Then_it_is_200_unreachable_with_the_reason(FetchFailureReason failure, string reason)
    {
        var address = $"https://lifepointfit.ca/failing/{reason}";
        factory.Sources.Answer(address, SourceFetchResult.Failed(failure));
        using var client = await StaffClient.OpenAsync(factory);

        using var response = await client.PostAsJsonAsync(TestAddress, new { kind = "rss", address }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.False(body.GetProperty("reachable").GetBoolean());
        Assert.Equal(reason, body.GetProperty("reason").GetString());
        Assert.Equal(0, body.GetProperty("items_found").GetInt32());
        Assert.Equal(0, body.GetProperty("items_relevant").GetInt32());
    }

    // L2-002 AC4 and L2-024: a host that is not public is never requested.
    [Fact]
    public async Task Given_a_host_that_is_not_public_When_it_is_tested_Then_it_is_unsafe_address_and_nothing_is_requested()
    {
        factory.Hosts.Answer("intranet.lifepointfit.ca", "10.1.2.3");
        using var client = await StaffClient.OpenAsync(factory);

        using var response = await client.PostAsJsonAsync(TestAddress, new { kind = "rss", address = "https://intranet.lifepointfit.ca/feed" }, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.False(body.GetProperty("reachable").GetBoolean());
        Assert.Equal("unsafe_address", body.GetProperty("reason").GetString());
        Assert.DoesNotContain(factory.Sources.Requests, request => request.Address.Host == "intranet.lifepointfit.ca");
    }

    [Theory]
    [InlineData("instagram")]
    [InlineData("eventbrite")]
    public async Task Given_a_platform_kind_with_no_credentials_When_it_is_tested_Then_it_is_refused_without_a_request(string kind)
    {
        using var client = await StaffClient.OpenAsync(factory);

        using var response = await client.PostAsJsonAsync(TestAddress, new { kind, address = $"https://{kind}.example/lifepoint" }, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.False(body.GetProperty("reachable").GetBoolean());
        Assert.Equal("refused", body.GetProperty("reason").GetString());
        Assert.DoesNotContain(factory.Sources.Requests, request => request.Address.Host == $"{kind}.example");
    }

    [Theory]
    [InlineData("kind", "rss2", "https://lifepointfit.ca/feed")]
    [InlineData("kind", "news_query", "https://lifepointfit.ca/feed")]
    [InlineData("address", "rss", "ftp://lifepointfit.ca/feed")]
    [InlineData("address", "rss", null)]
    public async Task Given_a_malformed_body_When_it_is_tested_Then_it_is_400_naming_the_field(string field, string kind, string? address)
    {
        using var client = await StaffClient.OpenAsync(factory);

        using var response = await client.PostAsJsonAsync(TestAddress, new { kind, address }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
        Assert.Equal([field], problem.GetProperty("errors").EnumerateObject().Select(entry => entry.Name));
    }

    // L2-002 AC3: a source in the catalogue is tested the same way; its health does not change.
    [Fact]
    public async Task Given_an_existing_source_When_it_is_tested_Then_the_result_is_the_same_and_its_health_is_unchanged()
    {
        await StaffClient.SeedAsync(factory);
        factory.Sources.AnswerFeed("https://lifepointfit.ca/existing/feed", Feed);
        using var client = await StaffClient.OpenAsync(factory);
        using var created = await client.PostAsJsonAsync(new Uri("/admin/v1/sources", UriKind.Relative), new
        {
            kind = "rss",
            address = "https://lifepointfit.ca/existing/feed",
            organization_name = "LifePoint Fitness",
            region = "toronto",
        }, TestContext.Current.CancellationToken);
        var before = await created.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var id = before.GetProperty("id").GetString();

        using var response = await client.PostAsync(new Uri($"/admin/v1/sources/{id}/test", UriKind.Relative), null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(body.GetProperty("reachable").GetBoolean());
        Assert.Equal(3, body.GetProperty("items_found").GetInt32());
        Assert.Equal(2, body.GetProperty("items_relevant").GetInt32());
        var after = await client.GetFromJsonAsync<JsonElement>(created.Headers.Location, TestContext.Current.CancellationToken);
        Assert.Equal(before.GetRawText(), after.GetRawText());
    }

    [Theory]
    [InlineData("src_aaaaaaaaaaaa")]
    [InlineData("not-an-id")]
    public async Task Given_no_source_with_the_id_When_it_is_tested_Then_it_is_404_source_not_found(string id)
    {
        using var client = await StaffClient.OpenAsync(factory);

        using var response = await client.PostAsync(new Uri($"/admin/v1/sources/{id}/test", UriKind.Relative), null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("source_not_found", problem.GetProperty("code").GetString());
    }

    // L2-023: a feed is untrusted input. No DTD is processed, no entity resolved, no unsafe link kept.
    [Fact]
    public async Task Given_a_feed_with_a_DTD_When_it_is_tested_Then_it_is_invalid_content()
    {
        const string withEntity = """
            <?xml version="1.0"?>
            <!DOCTYPE rss [<!ENTITY secret SYSTEM "file:///etc/passwd">]>
            <rss version="2.0"><channel><item><title>&secret;</title><link>https://lifepointfit.ca/x</link></item></channel></rss>
            """;
        factory.Sources.AnswerFeed("https://lifepointfit.ca/dtd/feed", withEntity);
        using var client = await StaffClient.OpenAsync(factory);

        var body = await TestAsync(client, "https://lifepointfit.ca/dtd/feed");

        Assert.False(body.GetProperty("reachable").GetBoolean());
        Assert.Equal("invalid_content", body.GetProperty("reason").GetString());
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,hello")]
    [InlineData("file:///etc/passwd")]
    [InlineData("/relative/path")]
    public async Task Given_an_item_whose_link_is_not_http_When_it_is_tested_Then_it_is_not_counted(string link)
    {
        var address = $"https://lifepointfit.ca/links/{Uri.EscapeDataString(link)}";
        factory.Sources.AnswerFeed(address, $"""
            <rss version="2.0"><channel>
              <item><title>Church fitness night</title><link>https://lifepointfit.ca/e/1</link></item>
              <item><title>Church fitness night</title><link>{link}</link></item>
            </channel></rss>
            """);
        using var client = await StaffClient.OpenAsync(factory);

        var body = await TestAsync(client, address);

        Assert.Equal(1, body.GetProperty("items_found").GetInt32());
    }

    [Fact]
    public async Task Given_a_feed_of_250_items_When_it_is_tested_Then_200_are_counted()
    {
        var items = string.Concat(Enumerable.Range(1, 250).Select(i => $"<item><title>Item {i}</title><link>https://lifepointfit.ca/e/{i}</link></item>"));
        factory.Sources.AnswerFeed("https://lifepointfit.ca/long/feed", $"<rss version=\"2.0\"><channel>{items}</channel></rss>");
        using var client = await StaffClient.OpenAsync(factory);

        var body = await TestAsync(client, "https://lifepointfit.ca/long/feed");

        Assert.Equal(200, body.GetProperty("items_found").GetInt32());
    }

    [Fact]
    public async Task Given_an_atom_feed_When_it_is_tested_Then_its_entries_are_counted()
    {
        await StaffClient.SeedAsync(factory);
        factory.Sources.AnswerFeed("https://lifepointfit.ca/atom", """
            <feed xmlns="http://www.w3.org/2005/Atom"><title>LifePoint</title>
              <entry><title>Church fitness night</title><link href="https://lifepointfit.ca/e/1"/><summary>Toronto</summary></entry>
              <entry><title>Council budget</title><link rel="alternate" href="https://lifepointfit.ca/e/2"/></entry>
            </feed>
            """);
        using var client = await StaffClient.OpenAsync(factory);

        var body = await TestAsync(client, "https://lifepointfit.ca/atom");

        Assert.Equal(2, body.GetProperty("items_found").GetInt32());
        Assert.Equal(1, body.GetProperty("items_relevant").GetInt32());
    }

    private static async Task<JsonElement> TestAsync(HttpClient client, string address)
    {
        using var response = await client.PostAsJsonAsync(TestAddress, new { kind = "rss", address }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    private async Task<int> CountAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAppDbContext>().Sources.CountAsync(TestContext.Current.CancellationToken);
    }
}
