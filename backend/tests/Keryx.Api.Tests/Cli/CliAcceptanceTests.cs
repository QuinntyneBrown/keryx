// Acceptance Test
// Traces to: L2-095
// Description: `migrate` applies what is pending and then nothing, `seed` leaves each seeded row exactly once, and an unknown command prints the usage

using Keryx.Api.Tests.Fakes;
using Keryx.Cli;
using Keryx.Domain.Sources;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Api.Tests.Cli;

public sealed class CliAcceptanceTests
{
    private static readonly string[] RegionSlugs =
    [
        "toronto", "scarborough", "mississauga", "brampton", "markham", "vaughan", "oakville",
        "richmond-hill", "pickering", "ajax", "gta", "ontario", "canada", "global",
    ];

    private static readonly string[] CategorySlugs =
        ["events", "races-challenges", "gyms-studios", "ministry-programs", "nutrition", "faith-wellness"];

    // L2-095 AC3. The migrations themselves need PostgreSQL: here they are answered by a fake,
    // which finds two pending the first time and none after.
    [Fact]
    public async Task Given_pending_migrations_When_migrate_runs_twice_Then_it_applies_them_once_and_exits_0_both_times()
    {
        var cli = new CliRun { Migrator = new RecordingMigrator(2) };

        var first = await cli.RunAsync("migrate");
        var second = await cli.RunAsync("migrate");

        Assert.Equal(new CliResult(0, $"Migrations applied: 2{Environment.NewLine}", string.Empty), first);
        Assert.Equal(new CliResult(0, $"Migrations applied: 0{Environment.NewLine}", string.Empty), second);
        Assert.Equal(2, cli.Migrator.Calls);
    }

    // L2-095 AC4, run once.
    [Fact]
    public async Task Given_an_empty_database_When_seed_runs_Then_the_regions_categories_settings_and_news_query_sources_exist()
    {
        var cli = new CliRun();

        var result = await cli.RunAsync("seed");

        Assert.Equal(
            new CliResult(0, $"Seed data added: regions 14, categories 6, settings 1, sources 1{Environment.NewLine}", string.Empty),
            result);
        await AssertSeededOnceAsync(cli, createdAt: new DateTimeOffset(2026, 11, 27, 15, 0, 0, TimeSpan.Zero));
        Assert.Equal(0, cli.Migrator.Calls);
    }

    // L2-095 AC4, run many times: each row exists exactly once, and is the first run's row.
    [Fact]
    public async Task Given_a_seeded_database_When_seed_runs_again_and_again_Then_each_row_exists_exactly_once_and_nothing_changes()
    {
        var cli = new CliRun();
        await cli.RunAsync("seed");

        cli.Time.Advance(TimeSpan.FromDays(1));
        var second = await cli.RunAsync("seed");
        cli.Time.Advance(TimeSpan.FromDays(1));
        var third = await cli.RunAsync("seed");

        var nothing = new CliResult(0, $"Seed data added: regions 0, categories 0, settings 0, sources 0{Environment.NewLine}", string.Empty);
        Assert.Equal(nothing, second);
        Assert.Equal(nothing, third);
        await AssertSeededOnceAsync(cli, createdAt: new DateTimeOffset(2026, 11, 27, 15, 0, 0, TimeSpan.Zero));
    }

    [Theory]
    [InlineData]
    [InlineData("bogus")]
    [InlineData("Migrate")]
    [InlineData("migrate", "seed")]
    [InlineData("--help")]
    public async Task Given_a_command_line_that_names_no_command_When_it_runs_Then_it_prints_the_usage_exits_1_and_changes_nothing(
        params string[] args)
    {
        var cli = new CliRun { Migrator = new RecordingMigrator(2) };

        var result = await cli.RunAsync(args);

        Assert.Equal(new CliResult(1, string.Empty, KeryxCli.Usage + Environment.NewLine), result);
        Assert.Equal(0, cli.Migrator.Calls);
        await using var db = TestStore.Open(cli.Store);
        Assert.Equal(0, await db.Regions.CountAsync(TestContext.Current.CancellationToken));
    }

    private static async Task AssertSeededOnceAsync(CliRun cli, DateTimeOffset createdAt)
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var db = TestStore.Open(cli.Store);

        Assert.Equal(RegionSlugs, await db.Regions.OrderBy(region => region.Rank).Select(region => region.Slug).ToListAsync(cancellation));
        Assert.Equal(CategorySlugs, await db.Categories.OrderBy(category => category.Rank).Select(category => category.Slug).ToListAsync(cancellation));

        var settings = Assert.Single(await db.Settings.ToListAsync(cancellation));
        Assert.Equal(1, settings.Id);
        Assert.Equal(6, settings.IntervalHours);
        Assert.Equal(0.60m, settings.PublishThreshold);
        Assert.Equal(24, settings.RetentionMonths);

        var source = Assert.Single(await db.Sources.ToListAsync(cancellation));
        Assert.Equal("toronto-church-fitness", source.SeedKey);
        Assert.Equal(SourceKind.NewsQuery, source.Kind);
        Assert.Equal("Toronto church fitness", source.OrganizationName);
        Assert.Equal("\"church\" AND (fitness OR run) near:Toronto", source.Address);
        Assert.Equal("gta", source.RegionSlug);
        Assert.Equal(createdAt, source.CreatedAt);
    }
}
