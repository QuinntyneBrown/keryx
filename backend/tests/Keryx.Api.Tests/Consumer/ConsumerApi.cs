using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using Keryx.Domain.Sources;
using Keryx.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Keryx.Api.Tests.Runs.CollectionRuns;

namespace Keryx.Api.Tests.Consumer;

/// <summary>A consuming product: its key, created by staff as in L2-035, and the published articles it reads.</summary>
internal static class ConsumerApi
{
    /// <summary>
    /// An item of a feed: a title, a link, a publication time (RFC 822) or none, and, for an event,
    /// its start and place in the RSS event module.
    /// </summary>
    public sealed record Item(string Title, string Link, DateTimeOffset? PublishedAt = null, string Summary = "All welcome.", string? EventStart = null, string? EventPlace = null);

    /// <summary>Creates a key with these scopes through the admin API, as staff would, and returns it.</summary>
    public static async Task<string> KeyAsync(ApiFactory factory, params string[] scopes)
    {
        using var staff = await StaffClient.OpenAsync(factory);
        using var response = await staff.PostAsJsonAsync(new Uri("/admin/v1/api-clients", UriKind.Relative),
            new { product_name = $"Product {Guid.NewGuid():N}", scopes }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("key").GetString()!;
    }

    /// <summary>A product's HTTP client: no cookie, and the key as a Bearer token when one is given.</summary>
    public static HttpClient Client(ApiFactory factory, string? key)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        if (key is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }

        return client;
    }

    /// <summary>Adds a source with these items and runs a collection, which publishes the relevant ones.</summary>
    public static async Task CollectAsync(ApiFactory factory, string address, string organization, string region, params Item[] items)
    {
        await AddSourceAsync(factory, SourceKind.Rss, address, organization, region);
        factory.Sources.AnswerFeed(address, Feed(items));
        await RunAsync(factory);
    }

    public static string Feed(params Item[] items) =>
        "<rss version=\"2.0\" xmlns:ev=\"http://purl.org/rss/1.0/modules/event/\"><channel><title>Feed</title>" +
        string.Concat(items.Select(item =>
            $"<item><title>{SecurityElement.Escape(item.Title)}</title><link>{SecurityElement.Escape(item.Link)}</link>" +
            $"<description>{SecurityElement.Escape(item.Summary)}</description>" +
            (item.PublishedAt is { } at ? $"<pubDate>{at.ToUniversalTime():R}</pubDate>" : string.Empty) +
            (item.EventStart is { } start ? $"<ev:startdate>{start}</ev:startdate>" : string.Empty) +
            (item.EventPlace is { } place ? $"<ev:location>{SecurityElement.Escape(place)}</ev:location>" : string.Empty) +
            "</item>")) +
        "</channel></rss>";

    /// <summary>An article's public id, read from the fixture's store, for a test to request it on /v1.</summary>
    public static async Task<string> ArticleIdAsync(ApiFactory factory, string url)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<KeryxDbContext>().Articles
            .Where(article => article.Url == url).Select(article => article.PublicId)
            .SingleAsync(TestContext.Current.CancellationToken);
    }
}
