using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Domain.Sources;
using Keryx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Keryx.Api.Tests.Consumer.ConsumerApi;
using static Keryx.Api.Tests.Runs.CollectionRuns;
using static Keryx.Api.Tests.Sources.EditSourceTests;

namespace Keryx.Api.Tests.Sources;

// Acceptance Test
// Traces to: L2-003 AC3, AC4, AC5; L2-065 AC4 (Undo)
// Description: Staff remove a source. It is no longer fetched or listed, while every record
// collected from it stays stored and served with its provenance; its open issues close. A source
// read shows how many articles came from it. Undo restores the same source unless an active one
// with its kind and address was added meanwhile; the same source added again matches the records
// it yielded before.
public sealed class RemoveSourceTests
{
    private const string Temple = "https://templefit.ca/feed";
    private const string Redeemed = "https://redeemedrun.ca/feed";
    private const string Item = "https://templefit.ca/1";

    [Fact]
    public async Task Given_a_source_with_an_article_When_staff_read_it_Then_it_counts_the_article_and_says_how_its_last_fetch_went()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        var (temple, redeemed) = await CollectAsync(factory);

        var read = await ReadSourceAsync(client, temple);
        var failing = await ReadSourceAsync(client, redeemed);

        Assert.Equal(
            ["id", "kind", "address", "organization_name", "region", "health", "failure_reason", "consecutive_failures", "article_count", "created_at", "last_fetched_at", "last_succeeded_at"],
            read.EnumerateObject().Select(property => property.Name));
        Assert.Equal(1, read.GetProperty("article_count").GetInt32());
        Assert.Equal(JsonValueKind.Null, read.GetProperty("failure_reason").ValueKind);
        Assert.Equal((0, "not_found", 1), (failing.GetProperty("article_count").GetInt32(), failing.GetProperty("failure_reason").GetString(), failing.GetProperty("consecutive_failures").GetInt32()));
    }

    [Fact]
    public async Task Given_a_source_with_an_article_When_staff_remove_it_Then_204_it_is_not_fetched_or_listed_and_its_article_is_still_served()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        var (temple, _) = await CollectAsync(factory);
        var articleId = await ArticleIdAsync(factory, Item);
        using var product = Client(factory, await KeyAsync(factory, "articles:read", "sources:read"));
        var before = await product.GetFromJsonAsync<JsonElement>(new Uri($"/v1/articles/{articleId}", UriKind.Relative), TestContext.Current.CancellationToken);

        using var response = await RemoveAsync(client, temple);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var read = await client.GetAsync(new Uri($"/admin/v1/sources/{temple}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        var list = await client.GetFromJsonAsync<JsonElement>(new Uri("/admin/v1/sources", UriKind.Relative), TestContext.Current.CancellationToken);
        // The failing source, then the seeded news query.
        Assert.Equal(["Redeemed Run Club", "Toronto church fitness"], list.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("organization_name").GetString()));
        Assert.Equal((2, 2, 1), (list.GetProperty("total").GetInt32(), list.GetProperty("all_count").GetInt32(), list.GetProperty("failing_count").GetInt32()));
        var after = await product.GetFromJsonAsync<JsonElement>(new Uri($"/v1/articles/{articleId}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(before.GetRawText(), after.GetRawText());
        var admin = await client.GetFromJsonAsync<JsonElement>(new Uri($"/admin/v1/articles/{articleId}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal((temple, "Temple Fit"), (admin.GetProperty("source").GetProperty("id").GetString(), admin.GetProperty("source").GetProperty("name").GetString()));
        var sources = await product.GetFromJsonAsync<JsonElement>(new Uri("/v1/sources", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(["Redeemed Run Club", "Toronto church fitness"], sources.GetProperty("data").EnumerateArray().Select(item => item.GetProperty("name").GetString()));
        var fetched = factory.Sources.Requests.Count;
        await RunAsync(factory);
        Assert.Equal([Redeemed], factory.Sources.Requests.Skip(fetched).Select(request => request.Address.OriginalString));
    }

    [Fact]
    public async Task Given_a_failing_source_When_staff_remove_it_Then_its_open_issue_closes_and_its_runs_still_name_it()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        var (_, redeemed) = await CollectAsync(factory);
        var runId = await LatestRunAsync(factory);

        using var response = await RemoveAsync(client, redeemed);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var issues = await client.GetFromJsonAsync<JsonElement>(new Uri("/admin/v1/issues", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Empty(issues.GetProperty("items").EnumerateArray());
        var run = await ReadRunAsync(client, runId);
        Assert.Equal(["Redeemed Run Club"], run.GetProperty("issues").EnumerateArray().Select(issue => issue.GetProperty("subject_name").GetString()));
    }

    [Fact]
    public async Task Given_a_removed_source_When_its_kind_and_address_are_added_again_Then_201_and_its_items_match_their_records()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        var (temple, _) = await CollectAsync(factory);
        using (var removed = await RemoveAsync(client, temple))
        {
            Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        }

        using var added = await client.PostAsJsonAsync(new Uri("/admin/v1/sources", UriKind.Relative), new { kind = "rss", address = Temple, organization_name = "Temple Fit", region = "toronto" }, TestContext.Current.CancellationToken);
        await RunAsync(factory);

        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var id = (await added.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetString()!;
        Assert.NotEqual(temple, id);
        Assert.Equal(1, await CountArticlesAsync(factory));
        // The article it matched now names it too.
        Assert.Equal(1, (await ReadSourceAsync(client, id)).GetProperty("article_count").GetInt32());
    }

    [Fact]
    public async Task Given_a_removed_source_When_staff_undo_Then_200_with_the_same_source_listed_and_fetched_again()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        var (temple, _) = await CollectAsync(factory);
        using (var removed = await RemoveAsync(client, temple))
        {
            Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        }

        using var response = await RestoreAsync(client, temple);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal((temple, 1), (body.GetProperty("id").GetString(), body.GetProperty("article_count").GetInt32()));
        Assert.Equal(body.GetRawText(), (await ReadSourceAsync(client, temple)).GetRawText());
        var fetched = factory.Sources.Requests.Count;
        await RunAsync(factory);
        Assert.Contains(Temple, factory.Sources.Requests.Skip(fetched).Select(request => request.Address.OriginalString));
    }

    [Fact]
    public async Task Given_the_same_source_added_after_a_removal_When_staff_undo_it_Then_409_source_exists_and_it_stays_removed()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        var (temple, _) = await CollectAsync(factory);
        using (var removed = await RemoveAsync(client, temple))
        {
            Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        }

        await AddSourceAsync(factory, SourceKind.Rss, "https://TempleFit.ca/feed/", "Temple Fit again", "toronto");

        using var response = await RestoreAsync(client, temple);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("source_exists", (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("code").GetString());
        using var read = await client.GetAsync(new Uri($"/admin/v1/sources/{temple}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
    }

    // L2-003 AC5: a removed source is gone to everything but Undo.
    [Theory]
    [InlineData("src_aaaaaaaaaaaa")]
    [InlineData("not-an-id")]
    [InlineData("removed")]
    public async Task Given_no_such_source_When_staff_remove_test_or_edit_it_Then_404_source_not_found(string id)
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        if (id == "removed")
        {
            id = await AddSourceAsync(factory, SourceKind.Rss, Temple, "Temple Fit", "toronto");
            using var first = await RemoveAsync(client, id);
            Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        }

        using var remove = await RemoveAsync(client, id);
        using var test = await client.PostAsync(new Uri($"/admin/v1/sources/{id}/test", UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        using var edit = await EditAsync(client, id, Temple, "Temple Fit", "toronto");

        foreach (var response in new[] { remove, test, edit })
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("source_not_found", (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("code").GetString());
        }
    }

    [Theory]
    [InlineData("src_aaaaaaaaaaaa")]
    [InlineData("not-an-id")]
    public async Task Given_no_such_source_When_staff_undo_Then_404_source_not_found(string id)
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);

        using var response = await RestoreAsync(client, id);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("source_not_found", (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("code").GetString());
    }

    /// <summary>Temple Fit yields one published article; Redeemed Run Club fails, raising an issue.</summary>
    private static async Task<(string Temple, string Redeemed)> CollectAsync(ApiFactory factory)
    {
        var temple = await AddSourceAsync(factory, SourceKind.Rss, Temple, "Temple Fit", "toronto");
        var redeemed = await AddSourceAsync(factory, SourceKind.Rss, Redeemed, "Redeemed Run Club", "toronto");
        factory.Sources.AnswerFeed(Temple, Runs.CollectionRuns.Feed(("Sunrise running club at St. Paul's church", Item, "Sunrise running club at St. Paul's church")));
        await RunAsync(factory);
        Assert.Equal(1, await CountArticlesAsync(factory));
        return (temple, redeemed);
    }

    private static Task<HttpResponseMessage> RemoveAsync(HttpClient client, string id) =>
        client.DeleteAsync(new Uri($"/admin/v1/sources/{id}", UriKind.Relative), TestContext.Current.CancellationToken);

    private static Task<HttpResponseMessage> RestoreAsync(HttpClient client, string id) =>
        client.PostAsync(new Uri($"/admin/v1/sources/{id}/restore", UriKind.Relative), content: null, TestContext.Current.CancellationToken);

    private static async Task<JsonElement> ReadSourceAsync(HttpClient client, string id) =>
        await client.GetFromJsonAsync<JsonElement>(new Uri($"/admin/v1/sources/{id}", UriKind.Relative), TestContext.Current.CancellationToken);

    private static async Task<int> CountArticlesAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<KeryxDbContext>().Articles.CountAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<string> LatestRunAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<KeryxDbContext>().Runs
            .OrderByDescending(run => run.Id).Select(run => run.PublicId).FirstAsync(TestContext.Current.CancellationToken);
    }
}
