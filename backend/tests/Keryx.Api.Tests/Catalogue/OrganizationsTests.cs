using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using Keryx.Domain.Sources;
using Keryx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Keryx.Api.Tests.Runs.CollectionRuns;

namespace Keryx.Api.Tests.Catalogue;

// Acceptance Test
// Traces to: L2-010 AC5; L2-011 AC1; L2-017
// Description: The organization behind a source is stored once, however each source spells its
// name, with a kind, a region, its public website and where it was seen; its articles name it.
public sealed class OrganizationsTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    [Fact]
    public async Task Given_one_organization_named_two_ways_by_two_sources_When_both_are_collected_Then_one_organization_exists_with_both_in_its_provenance()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        var feed = await AddSourceAsync(factory, SourceKind.Rss, "https://redeemedrun.ca/feed", "The Redeemed Run Club", "toronto");
        var site = await AddSourceAsync(factory, SourceKind.Rss, "https://www.redeemedrun.ca/events.xml", "redeemed  run-club.", "toronto");
        factory.Sources.AnswerFeed("https://redeemedrun.ca/feed", Feed(("Toronto church running club starts spring 5K training", "https://redeemedrun.ca/news/5k", "Weekly runs.")));
        factory.Sources.AnswerFeed("https://www.redeemedrun.ca/events.xml", Feed(("Sunrise prayer run along the Toronto waterfront", "https://redeemedrun.ca/events/sunrise", "Bring a friend.")));
        var seen = factory.Time.GetUtcNow();

        await RunAsync(factory);
        factory.Time.Advance(Interval);
        await RunAsync(factory);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
        var organization = Assert.Single(await db.Organizations.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken));
        Assert.StartsWith("org_", organization.PublicId, StringComparison.Ordinal);
        Assert.Equal("The Redeemed Run Club", organization.Name);
        Assert.Equal(Keryx.Domain.Catalogue.OrganizationKind.Club, organization.Kind);
        Assert.Equal("toronto", organization.RegionSlug);
        Assert.Equal("https://redeemedrun.ca", organization.WebsiteUrl);
        Assert.Equal(2, organization.Provenance.Count);
        Assert.All(organization.Provenance, entry =>
        {
            Assert.Equal(seen, entry.FirstSeenAt);
            Assert.Equal(seen + Interval, entry.LastSeenAt);
        });
        var sourceIds = await db.Sources.AsNoTracking().Where(source => source.PublicId == feed || source.PublicId == site)
            .Select(source => (long?)source.Id).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(sourceIds.Order(), organization.Provenance.Select(entry => entry.SourceId).Order());

        using var client = await StaffClient.OpenAsync(factory);
        foreach (var id in await db.Articles.Select(article => article.PublicId).ToListAsync(TestContext.Current.CancellationToken))
        {
            var article = await client.GetFromJsonAsync<JsonElement>(new Uri($"/admin/v1/articles/{id}", UriKind.Relative), TestContext.Current.CancellationToken);
            Assert.Equal("The Redeemed Run Club", article.GetProperty("organization").GetString());
        }
    }

    [Fact]
    public async Task Given_two_different_organizations_When_both_are_collected_Then_each_is_its_own_record()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, "https://redeemedrun.ca/feed", "Redeemed Run Club", "toronto");
        await AddSourceAsync(factory, SourceKind.Rss, "https://lifepointfit.ca/feed", "LifePoint Fitness", "mississauga");
        factory.Sources.AnswerFeed("https://redeemedrun.ca/feed", Feed(("Toronto church running club starts spring 5K training", "https://redeemedrun.ca/news/5k", "Weekly runs.")));
        factory.Sources.AnswerFeed("https://lifepointfit.ca/feed", Feed(("Faith and fitness bootcamp in Mississauga", "https://lifepointfit.ca/e/1", "Bring water.")));

        await RunAsync(factory);

        await using var scope = factory.Services.CreateAsyncScope();
        var names = await scope.ServiceProvider.GetRequiredService<KeryxDbContext>().Organizations
            .OrderBy(organization => organization.Name).Select(organization => organization.Name).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["LifePoint Fitness", "Redeemed Run Club"], names);
    }
}
