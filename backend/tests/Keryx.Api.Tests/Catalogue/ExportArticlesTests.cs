using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using static Keryx.Api.Tests.Consumer.ConsumerApi;

namespace Keryx.Api.Tests.Catalogue;

// Acceptance Test
// Traces to: L2-054 AC2, AC4
// Description: Staff download the articles matching the current status, search, region and
// category as a CSV file named for the Toronto date, with the columns id, title, organization,
// category, region, relevance, status, published_at, collected_at and url, newest collected
// first. Every value is quoted, and a value a spreadsheet would run as a formula is guarded.
public sealed class ExportArticlesTests
{
    private static readonly DateTimeOffset Day = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    private static readonly string[] Columns =
        ["id", "title", "organization", "category", "region", "relevance", "status", "published_at", "collected_at", "url"];

    // The list tests' seven articles, then the clock moves to 3 AM UTC on Nov 28: still Nov 27
    // in Toronto. Staff sign in after the move, so the session is fresh.
    private static async Task<HttpClient> SeededAsync(ApiFactory factory)
    {
        await StaffClient.SeedAsync(factory);
        await CollectAsync(factory, "https://redeemedrun.ca/feed", "Redeemed Run Club", "toronto",
            new Item("Running apple", "https://redeemedrun.ca/n/apple", Day),
            new Item("Running birch", "https://redeemedrun.ca/n/birch", Day),
            new Item("Community bake sale", "https://redeemedrun.ca/n/bake", Day),
            new Item(AdminArticlesListTests.LongTitle, "https://redeemedrun.ca/n/long", Day));
        factory.Time.Advance(TimeSpan.FromMinutes(1));
        await CollectAsync(factory, "https://worldfaithfit.org/feed", "World Faith Fit", "global",
            new Item("Running and faith retreat", "https://worldfaithfit.org/n/1", Day),
            new Item("Faith and running in Lagos", "https://worldfaithfit.org/n/2", Day));
        factory.Time.Advance(TimeSpan.FromMinutes(1));
        await CollectAsync(factory, "https://lifepointfit.ca/feed", "LifePoint Fitness", "mississauga",
            new Item("Running clinic at Port Credit", "https://lifepointfit.ca/n/clinic", Day));
        factory.Time.Advance(new DateTimeOffset(2026, 11, 28, 3, 0, 0, TimeSpan.Zero) - factory.Time.GetUtcNow());
        return await StaffClient.OpenAsync(factory);
    }

    [Fact]
    public async Task Given_seven_articles_When_staff_export_Then_a_csv_named_for_the_Toronto_date_holds_every_row_newest_collected_first()
    {
        using var factory = new ApiFactory();
        using var staff = await SeededAsync(factory);
        var listed = await ListAsync(staff);

        using var response = await staff.GetAsync(new Uri("/admin/v1/articles/export", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("articles-2026-11-27.csv", response.Content.Headers.ContentDisposition?.FileName);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var rows = Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(Columns, rows[0]);
        Assert.Equal(listed.Select(item => item.GetProperty("id").GetString()), rows.Skip(1).Select(row => row[0]));

        var clinic = listed[0];
        Assert.Equal(
            [
                clinic.GetProperty("id").GetString()!, "Running clinic at Port Credit", "LifePoint Fitness", "events", "mississauga", "0.60",
                "published", "2026-11-26T12:00:00Z", "2026-11-27T15:02:00Z", "https://lifepointfit.ca/n/clinic",
            ],
            rows[1]);
        var apple = rows[^1];
        Assert.Equal(
            ["Running apple", "Redeemed Run Club", "toronto", "2026-11-26T12:00:00Z", "2026-11-27T15:00:00Z", "https://redeemedrun.ca/n/apple"],
            [apple[1], apple[2], apple[4], apple[7], apple[8], apple[9]]);
    }

    [Theory]
    [MemberData(nameof(AdminArticlesListTests.Narrowed), MemberType = typeof(AdminArticlesListTests))]
    public async Task Given_a_status_region_category_or_search_When_staff_export_Then_the_file_holds_the_articles_the_list_shows(string query, string[] titles)
    {
        using var factory = new ApiFactory();
        using var staff = await SeededAsync(factory);

        using var response = await staff.GetAsync(new Uri($"/admin/v1/articles/export?{query}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(Columns, rows[0]);
        Assert.Equal(titles, rows.Skip(1).Select(row => row[1]));
    }

    [Fact]
    public async Task Given_titles_and_names_a_spreadsheet_would_run_When_staff_export_Then_each_is_guarded_and_quoted()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await CollectAsync(factory, "https://plusfit.ca/feed", "+Plus Fitness, \"Toronto\"", "toronto",
            new Item("=HYPERLINK(\"https://evil.test\",\"Run\") running", "https://plusfit.ca/n/1"));
        using var staff = await StaffClient.OpenAsync(factory);

        using var response = await staff.GetAsync(new Uri("/admin/v1/articles/export", UriKind.Relative), TestContext.Current.CancellationToken);

        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var line = text.Split("\r\n")[1];
        Assert.Contains("\"'=HYPERLINK(\"\"https://evil.test\"\",\"\"Run\"\") running\",\"'+Plus Fitness, \"\"Toronto\"\"\"", line, StringComparison.Ordinal);
        // The commas and quotes inside the values split nothing: the row still has ten cells.
        Assert.Equal(10, Parse(text)[1].Length);
        Assert.StartsWith("\"", line, StringComparison.Ordinal);
        Assert.EndsWith("\"", line, StringComparison.Ordinal);
        // It has no publication time: an empty cell, not a word.
        Assert.Equal(string.Empty, Parse(text)[1][7]);
    }

    [Fact]
    public async Task Given_no_staff_session_When_the_export_is_requested_Then_it_is_refused()
    {
        using var factory = new ApiFactory();
        using var anonymous = factory.CreateClient();

        using var response = await anonymous.GetAsync(new Uri("/admin/v1/articles/export", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<List<JsonElement>> ListAsync(HttpClient staff)
    {
        var page = await staff.GetFromJsonAsync<JsonElement>(new Uri("/admin/v1/articles?page_size=100", UriKind.Relative), TestContext.Current.CancellationToken);
        return [.. page.GetProperty("items").EnumerateArray()];
    }

    /// <summary>Reads RFC 4180 text: quoted fields, doubled quotes, CRLF between records.</summary>
    private static List<string[]> Parse(string text)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') { quoted = false; }
                else { field.Append(c); }
            }
            else if (c == '"') { quoted = true; }
            else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
            {
                row.Add(field.ToString()); field.Clear(); rows.Add([.. row]); row.Clear(); i++;
            }
            else { field.Append(c); }
        }

        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add([.. row]); }
        return rows;
    }
}
