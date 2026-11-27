using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using static Keryx.Api.Tests.Consumer.ConsumerApi;

namespace Keryx.Api.Tests.Catalogue;

// Acceptance Test
// Traces to: L2-048 AC5, L2-049 AC1-AC2, L2-050 AC2, AC3, AC5
// Description: Staff page through every collected article newest first, by status, search,
// region and category, and read the real total of each status.
public sealed class AdminArticlesListTests
{
    private static readonly DateTimeOffset Day = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly string LongTitle = "Running " + new string('k', 120);

    // One topic in Toronto scores 0.60 and is published; two topics far away score 0.48 and are
    // held for review; no topic scores 0.30 and is hidden. Each collection is a minute later.
    private static async Task<HttpClient> SeededAsync(ApiFactory factory)
    {
        await StaffClient.SeedAsync(factory);
        await CollectAsync(factory, "https://redeemedrun.ca/feed", "Redeemed Run Club", "toronto",
            new Item("Running apple", "https://redeemedrun.ca/n/apple", Day),
            new Item("Running birch", "https://redeemedrun.ca/n/birch", Day),
            new Item("Community bake sale", "https://redeemedrun.ca/n/bake", Day),
            new Item(LongTitle, "https://redeemedrun.ca/n/long", Day));
        factory.Time.Advance(TimeSpan.FromMinutes(1));
        await CollectAsync(factory, "https://worldfaithfit.org/feed", "World Faith Fit", "global",
            new Item("Running and faith retreat", "https://worldfaithfit.org/n/1", Day),
            new Item("Faith and running in Lagos", "https://worldfaithfit.org/n/2", Day));
        factory.Time.Advance(TimeSpan.FromMinutes(1));
        await CollectAsync(factory, "https://lifepointfit.ca/feed", "LifePoint Fitness", "mississauga",
            new Item("Running clinic at Port Credit", "https://lifepointfit.ca/n/clinic", Day));
        return await StaffClient.OpenAsync(factory);
    }

    [Fact]
    public async Task Given_seven_articles_When_the_first_page_is_read_Then_all_come_newest_collected_first_with_the_row_fields_and_numeric_paging()
    {
        using var factory = new ApiFactory();
        using var staff = await SeededAsync(factory);

        var page = await ListAsync(staff, string.Empty);

        Assert.Equal(["items", "page", "page_size", "total"], page.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal((1, 10, 7), (page.GetProperty("page").GetInt32(), page.GetProperty("page_size").GetInt32(), page.GetProperty("total").GetInt32()));
        Assert.Equal(
            ["Running clinic at Port Credit", "Faith and running in Lagos", "Running and faith retreat", LongTitle, "Community bake sale", "Running birch", "Running apple"],
            Titles(page));
        var clinic = page.GetProperty("items")[0];
        Assert.Equal(
            ["category", "collected_at", "id", "organization", "published_at", "region", "relevance", "status", "title"],
            clinic.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal(
            ("LifePoint Fitness", "events", "mississauga", 0.60m, "published"),
            (clinic.GetProperty("organization").GetString(), clinic.GetProperty("category").GetString(), clinic.GetProperty("region").GetString(),
             clinic.GetProperty("relevance").GetDecimal(), clinic.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task Given_a_page_size_of_2_When_page_2_is_read_Then_it_holds_the_third_and_fourth_newest()
    {
        using var factory = new ApiFactory();
        using var staff = await SeededAsync(factory);

        var page = await ListAsync(staff, "page=2&page_size=2");

        Assert.Equal(["Running and faith retreat", LongTitle], Titles(page));
        Assert.Equal((2, 2, 7), (page.GetProperty("page").GetInt32(), page.GetProperty("page_size").GetInt32(), page.GetProperty("total").GetInt32()));
    }

    public static TheoryData<string, string[]> Narrowed => new()
    {
        { "status=needs_review", ["Faith and running in Lagos", "Running and faith retreat"] },
        { "status=hidden", ["Community bake sale"] },
        { "status=published&region=toronto", [LongTitle, "Running birch", "Running apple"] },
        { "category=events", ["Running clinic at Port Credit", "Running and faith retreat"] },
        { "region=global&status=published", [] },
        // Title, organization name and place (the region's name) are searched, without regard to case.
        { "q=apple", ["Running apple"] },
        { "q=WORLD%20FAITH", ["Faith and running in Lagos", "Running and faith retreat"] },
        { "q=mississauga", ["Running clinic at Port Credit"] },
        { "q=faith&status=needs_review&region=global", ["Faith and running in Lagos", "Running and faith retreat"] },
        // A term is literal text: % and _ and a quote match only themselves.
        { "q=%25", [] },
        { "q=r_n", [] },
        { "q=%27apple", [] },
        // Only the first 100 characters are used: these 100 k's match, the extra Q does not count.
        { "q=" + new string('k', 100) + "Q", [LongTitle] },
    };

    [Theory]
    [MemberData(nameof(Narrowed))]
    public async Task Given_a_status_region_category_or_search_When_the_list_is_read_Then_only_matching_articles_come_and_the_total_counts_them(string query, string[] titles)
    {
        using var factory = new ApiFactory();
        using var staff = await SeededAsync(factory);

        var page = await ListAsync(staff, query);

        Assert.Equal(titles, Titles(page));
        Assert.Equal(titles.Length, page.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Given_an_article_of_an_event_When_its_place_is_searched_Then_it_is_found()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await CollectAsync(factory, "https://redeemedrun.ca/feed", "Redeemed Run Club", "toronto",
            new Item("Running cedar", "https://redeemedrun.ca/n/cedar", Day, EventStart: "2026-12-05T09:00:00-05:00", EventPlace: "Trinity Bellwoods"),
            new Item("Running apple", "https://redeemedrun.ca/n/apple", Day));
        using var staff = await StaffClient.OpenAsync(factory);

        Assert.Equal(["Running cedar"], Titles(await ListAsync(staff, "q=bellwoods")));
    }

    [Theory]
    [InlineData("page_size=0", "invalid_page")]
    [InlineData("page_size=101", "invalid_page")]
    [InlineData("page_size=ten", "invalid_page")]
    [InlineData("page=0", "invalid_page")]
    [InlineData("page=-1", "invalid_page")]
    [InlineData("status=draft", "invalid_filter")]
    [InlineData("region=atlantis", "invalid_filter")]
    [InlineData("category=yoga", "invalid_filter")]
    public async Task Given_a_value_it_cannot_use_When_the_list_is_read_Then_400_with_its_code(string query, string code)
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        using var staff = await StaffClient.OpenAsync(factory);

        using var response = await staff.GetAsync(new Uri($"/admin/v1/articles?{query}", UriKind.Relative), TestContext.Current.CancellationToken);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal((HttpStatusCode.BadRequest, code), (response.StatusCode, problem.GetProperty("code").GetString()));
    }

    [Fact]
    public async Task Given_articles_of_each_status_When_the_counts_are_read_Then_they_are_the_whole_totals_whatever_the_search()
    {
        using var factory = new ApiFactory();
        using var staff = await SeededAsync(factory);

        foreach (var query in new[] { string.Empty, "?q=faith&region=global" })
        {
            using var response = await staff.GetAsync(new Uri($"/admin/v1/articles/counts{query}", UriKind.Relative), TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var counts = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
            Assert.Equal(["all", "hidden", "needs_review", "published"], counts.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
            Assert.Equal((7, 2, 4, 1),
                (counts.GetProperty("all").GetInt32(), counts.GetProperty("needs_review").GetInt32(), counts.GetProperty("published").GetInt32(), counts.GetProperty("hidden").GetInt32()));
        }
    }

    [Theory]
    [InlineData("/admin/v1/articles")]
    [InlineData("/admin/v1/articles/counts")]
    public async Task Given_no_staff_session_When_articles_are_read_Then_401(string path)
    {
        using var factory = new ApiFactory();
        using var anonymous = factory.CreateClient();

        using var response = await anonymous.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static IEnumerable<string?> Titles(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("title").GetString());

    private static async Task<JsonElement> ListAsync(HttpClient staff, string query)
    {
        using var response = await staff.GetAsync(new Uri($"/admin/v1/articles?{query}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }
}
