using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Fakes;
using Keryx.Api.Tests.Session;
using Keryx.Domain.Catalogue;
using Keryx.Domain.Collection;

namespace Keryx.Api.Tests.Catalogue;

/// <summary>50,000 events articles and one about nutrition, stored once for the class.</summary>
public sealed class ExportBoundFixture : IAsyncLifetime
{
    public const int Bound = 50_000;

    public ApiFactory Factory { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await StaffClient.SeedAsync(Factory);
        var seen = Factory.Time.GetUtcNow();
        await using var store = TestStore.Open(Factory.Store);
        for (var i = 0; i <= Bound; i++)
        {
            var category = i < Bound ? "events" : "nutrition";
            store.Articles.Add(Article.Collect(
                new ArticleFacts($"https://example.org/{i}", $"Article {i}", "All welcome.", category, "toronto", [], null, 0.70m, PublicationStatus.Published),
                null, 1, "Example feed", seen));
        }

        await store.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        Factory.Dispose();
        return ValueTask.CompletedTask;
    }
}

// Acceptance Test
// Traces to: L2-054 AC2, AC3
// Description: An export holds at most 50,000 rows. When more match, it is refused with 400 and
// the code export_too_large; narrowed to 50,000, the whole file comes.
public sealed class ExportBoundTests(ExportBoundFixture fixture) : IClassFixture<ExportBoundFixture>
{
    [Fact]
    public async Task Given_50001_matching_articles_When_staff_export_Then_it_is_refused_as_too_large()
    {
        using var staff = await StaffClient.OpenAsync(fixture.Factory);

        using var response = await staff.GetAsync(new Uri("/admin/v1/articles/export", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("export_too_large", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Given_exactly_50000_matching_articles_When_staff_export_Then_every_row_comes()
    {
        using var staff = await StaffClient.OpenAsync(fixture.Factory);

        using var response = await staff.GetAsync(new Uri("/admin/v1/articles/export?category=events", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1 + ExportBoundFixture.Bound, text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
    }
}
