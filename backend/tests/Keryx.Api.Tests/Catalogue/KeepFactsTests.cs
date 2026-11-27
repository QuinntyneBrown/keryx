using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using Keryx.Application.Sources;
using Keryx.Domain.Sources;
using Keryx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Keryx.Api.Tests.Runs.CollectionRuns;

namespace Keryx.Api.Tests.Catalogue;

// Acceptance Test
// Traces to: L2-011 AC2; L2-080 AC1, AC3, AC4; L2-081 AC3
// Description: Only bounded facts and a link are stored: an item with no usable link is counted
// as rejected and not stored, a page that asks not to be indexed yields nothing, and an image or
// a contact detail in an item is not kept.
public sealed class KeepFactsTests
{
    private const string Address = "https://lifepointfit.ca/feed";

    [Fact]
    public async Task Given_items_with_no_usable_link_When_the_run_finishes_Then_they_are_counted_rejected_and_not_stored()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, Address, "LifePoint Fitness", "toronto");
        factory.Sources.AnswerFeed(Address,
            "<rss version=\"2.0\"><channel><title>Feed</title>" +
            "<item><title>Toronto church fitness night</title><link>https://lifepointfit.ca/e/1</link></item>" +
            "<item><title>No link at all</title></item>" +
            "<item><title>A script link</title><link>javascript:alert(1)</link></item>" +
            "</channel></rss>");

        var runId = await RunAsync(factory);

        using var client = await StaffClient.OpenAsync(factory);
        var run = await ReadRunAsync(client, runId);
        Assert.Equal(2, run.GetProperty("rejected").GetInt32());
        Assert.Equal(1, run.GetProperty("added").GetInt32());
        var deduplicate = run.GetProperty("stages").EnumerateArray().Single(stage => stage.GetProperty("stage").GetString() == "deduplicate");
        Assert.Equal(2, deduplicate.GetProperty("counts").GetProperty("rejected").GetInt32());
        Assert.Equal(["https://lifepointfit.ca/e/1"], await UrlsAsync(factory));
    }

    [Fact]
    public async Task Given_a_source_whose_page_asks_not_to_be_indexed_When_the_run_finishes_Then_nothing_from_it_is_stored()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, Address, "LifePoint Fitness", "toronto");
        factory.Sources.Answer(Address, SourceFetchResult.Fetched(
            Feed(("Toronto church fitness night", "https://lifepointfit.ca/e/1", "Join us.")), "application/rss+xml") with
        { NoIndex = true });

        var runId = await RunAsync(factory);

        using var client = await StaffClient.OpenAsync(factory);
        var run = await ReadRunAsync(client, runId);
        Assert.Equal((0, 1, 0), (run.GetProperty("added").GetInt32(), run.GetProperty("sources_ok").GetInt32(), run.GetProperty("sources_failed").GetInt32()));
        Assert.Empty(await UrlsAsync(factory));
    }

    [Fact]
    public async Task Given_an_item_with_an_image_and_contact_details_When_it_is_stored_Then_only_its_facts_and_link_are_kept()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, Address, "LifePoint Fitness", "toronto");
        factory.Sources.AnswerFeed(Address,
            "<rss version=\"2.0\" xmlns:media=\"http://search.yahoo.com/mrss/\"><channel><title>Feed</title><item>" +
            "<title>Toronto church fitness night</title><link>https://lifepointfit.ca/e/1</link>" +
            "<description>&lt;img src=\"https://lifepointfit.ca/a.jpg\"&gt;Bring a friend. Email pastor.dan@gmail.com or call 416-555-0199.</description>" +
            "<enclosure url=\"https://lifepointfit.ca/b.jpg\" type=\"image/jpeg\" length=\"1\"/>" +
            "<media:content url=\"https://lifepointfit.ca/c.jpg\" medium=\"image\"/>" +
            "</item></channel></rss>");

        await RunAsync(factory);

        using var client = await StaffClient.OpenAsync(factory);
        var id = await IdAsync(factory);
        var article = await client.GetFromJsonAsync<JsonElement>(new Uri($"/admin/v1/articles/{id}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal("Bring a friend. Email or call.", article.GetProperty("summary").GetString());
        Assert.DoesNotContain(".jpg", article.GetRawText(), StringComparison.Ordinal);
        // Exactly these fields: no body, image or HTML field exists to fill.
        Assert.Equal(
            ["category", "collected_at", "id", "organization", "provenance", "published_at", "region", "relevance", "source", "status", "summary", "tags", "title", "url"],
            article.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
    }

    private static async Task<List<string>> UrlsAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<KeryxDbContext>().Articles.Select(article => article.Url).ToListAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<string> IdAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<KeryxDbContext>().Articles.Select(article => article.PublicId).SingleAsync(TestContext.Current.CancellationToken);
    }
}
