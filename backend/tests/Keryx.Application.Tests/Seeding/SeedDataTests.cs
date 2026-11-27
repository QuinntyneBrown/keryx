// Acceptance Test
// Traces to: L2-095, L2-015, L2-018, L2-070
// Description: The seed data is the regions, the categories, the default settings and the news query sources, row by row

using Keryx.Domain.Seeding;
using Keryx.Domain.Sources;

namespace Keryx.Application.Tests.Seeding;

public sealed class SeedDataTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 15, 0, 0, TimeSpan.Zero);

    // One row per region, in rank order: Toronto first, the GTA municipalities, then the wider areas.
    [Theory]
    [InlineData(0, "toronto", "Toronto", "America/Toronto")]
    [InlineData(1, "scarborough", "Scarborough", "America/Toronto")]
    [InlineData(2, "mississauga", "Mississauga", "America/Toronto")]
    [InlineData(3, "brampton", "Brampton", "America/Toronto")]
    [InlineData(4, "markham", "Markham", "America/Toronto")]
    [InlineData(5, "vaughan", "Vaughan", "America/Toronto")]
    [InlineData(6, "oakville", "Oakville", "America/Toronto")]
    [InlineData(7, "richmond-hill", "Richmond Hill", "America/Toronto")]
    [InlineData(8, "pickering", "Pickering", "America/Toronto")]
    [InlineData(9, "ajax", "Ajax", "America/Toronto")]
    [InlineData(10, "gta", "Greater Toronto Area", "America/Toronto")]
    [InlineData(11, "ontario", "Ontario", "America/Toronto")]
    [InlineData(12, "canada", "Canada", "America/Toronto")]
    [InlineData(13, "global", "Global", "Etc/UTC")]
    public void Given_the_seed_data_Then_the_region_at_each_place_is(int place, string slug, string name, string timeZone)
    {
        var regions = SeedData.Regions().OrderBy(region => region.Rank).ToList();

        Assert.Equal(14, regions.Count);
        Assert.Equal((slug, name, timeZone), (regions[place].Slug, regions[place].Name, regions[place].TimeZone));
        // Every zone is one the platform knows.
        Assert.Equal(timeZone, TimeZoneInfo.FindSystemTimeZoneById(regions[place].TimeZone).Id);
    }

    [Fact]
    public void Given_the_seed_data_Then_no_two_regions_share_a_slug_or_a_rank()
    {
        var regions = SeedData.Regions();

        Assert.Equal(regions.Count, regions.Select(region => region.Slug).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(regions.Count, regions.Select(region => region.Rank).Distinct().Count());
    }

    // One row per category, in the order of L2-015.
    [Theory]
    [InlineData(0, "events", "Events")]
    [InlineData(1, "races-challenges", "Races & Challenges")]
    [InlineData(2, "gyms-studios", "Gyms & Studios")]
    [InlineData(3, "ministry-programs", "Ministry Programs")]
    [InlineData(4, "nutrition", "Nutrition")]
    [InlineData(5, "faith-wellness", "Faith & Wellness")]
    public void Given_the_seed_data_Then_the_category_at_each_place_is(int place, string slug, string name)
    {
        var categories = SeedData.Categories().OrderBy(category => category.Rank).ToList();

        Assert.Equal(6, categories.Count);
        Assert.Equal((slug, name), (categories[place].Slug, categories[place].Name));
    }

    [Fact]
    public void Given_the_seed_data_Then_the_default_settings_are_the_values_the_mocks_show()
    {
        var settings = SeedData.DefaultSettings();

        Assert.Equal(1, settings.Id);
        Assert.Equal(6, settings.IntervalHours);
        Assert.Equal(30, settings.TimeoutSeconds);
        Assert.Equal(0.60m, settings.PublishThreshold);
        Assert.Equal(["fitness", "running", "strength", "sports ministry", "wellness", "church", "faith", "nutrition"], settings.Topics);
        Assert.Equal(["casino", "supplement ads", "sponsored"], settings.Exclusions);
        Assert.Equal(
            ["Toronto", "Mississauga", "Brampton", "Markham", "Vaughan", "Oakville", "Richmond Hill", "Pickering", "Ajax"],
            settings.FocusAreas);
        Assert.True(settings.IncludeOntarioWide);
        Assert.False(settings.IncludeNational);
        Assert.True(settings.MergeDuplicates);
        Assert.Equal(24, settings.RetentionMonths);
        Assert.Null(settings.AlertEmail);
        Assert.Equal(3, settings.AlertFailureStreak);
        Assert.True(settings.AlertOnRunFailure);
        Assert.True(settings.DailyDigest);
    }

    [Fact]
    public void Given_the_seed_data_Then_every_default_focus_area_is_a_seeded_region()
    {
        var regionNames = SeedData.Regions().Select(region => region.Name).ToList();

        Assert.All(SeedData.DefaultSettings().FocusAreas, area => Assert.Contains(area, regionNames));
    }

    [Fact]
    public void Given_the_seed_data_Then_the_news_query_source_is_the_one_the_mocks_show_in_a_seeded_region()
    {
        var source = Assert.Single(SeedData.NewsQuerySources(Now));

        Assert.Equal("toronto-church-fitness", source.SeedKey);
        Assert.Equal(SourceKind.NewsQuery, source.Kind);
        Assert.Equal("Toronto church fitness", source.OrganizationName);
        Assert.Equal("\"church\" AND (fitness OR run) near:Toronto", source.Address);
        Assert.Equal("gta", source.RegionSlug);
        Assert.Equal(Now, source.CreatedAt);
        Assert.Contains(source.RegionSlug, SeedData.Regions().Select(region => region.Slug));
    }
}
