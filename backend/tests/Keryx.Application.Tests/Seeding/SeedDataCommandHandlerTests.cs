// Acceptance Test
// Traces to: L2-095
// Description: Seeding adds only the rows that are missing, and leaves a row that already exists as it is

using Keryx.Application.Abstractions;
using Keryx.Application.Seeding;
using Keryx.Domain;
using Keryx.Domain.Reference;
using Keryx.Domain.Sources;
using Keryx.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Keryx.Application.Tests.Seeding;

public sealed class SeedDataCommandHandlerTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 11, 27, 15, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _time = new(Start);
    private readonly ServiceProvider _services;

    public SeedDataCommandHandlerTests()
    {
        var root = new InMemoryDatabaseRoot();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(_time);
        services.AddApplication();
        services.AddDbContext<KeryxDbContext>(options => options.UseInMemoryDatabase("seed", root));
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<KeryxDbContext>());
        _services = services.BuildServiceProvider();
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();

    [Fact]
    public async Task Given_an_empty_database_When_it_is_seeded_Then_every_row_is_added_and_counted()
    {
        var result = await SeedAsync();

        Assert.Equal(new SeedDataResult(Regions: 14, Categories: 6, Settings: 1, Sources: 1), result);
        Assert.Equal(22, result.Total);
        Assert.Equal((14, 6, 1, 1), await CountsAsync());
    }

    [Fact]
    public async Task Given_a_seeded_database_When_it_is_seeded_again_later_Then_nothing_is_added_and_nothing_changes()
    {
        await SeedAsync();
        _time.Advance(TimeSpan.FromDays(3));

        var result = await SeedAsync();

        Assert.Equal(new SeedDataResult(Regions: 0, Categories: 0, Settings: 0, Sources: 0), result);
        Assert.Equal((14, 6, 1, 1), await CountsAsync());
        await using var scope = _services.CreateAsyncScope();
        var source = await scope.ServiceProvider.GetRequiredService<IAppDbContext>().Sources.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(Start, source.CreatedAt);
    }

    // A row staff have since changed keeps its change: seeding never overwrites.
    [Fact]
    public async Task Given_rows_that_already_exist_under_seeded_keys_When_it_is_seeded_Then_they_are_left_as_they_are_and_only_the_rest_is_added()
    {
        await using (var arrange = _services.CreateAsyncScope())
        {
            var db = arrange.ServiceProvider.GetRequiredService<IAppDbContext>();
            db.Regions.Add(new Region("toronto", "City of Toronto", "America/Toronto", rank: 5));
            db.Regions.Add(new Region("gta", "GTA", "America/Toronto", rank: 999));
            db.Categories.Add(new Category("events", "Happenings", rank: 9));
            db.Settings.Add(new Settings { IntervalHours = 12, RetentionMonths = 6 });
            db.Sources.Add(Source.SeededNewsQuery("toronto-church-fitness", "Renamed by staff", "\"church\" AND gym", "gta", Start.AddDays(-30)));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var result = await SeedAsync();

        Assert.Equal(new SeedDataResult(Regions: 12, Categories: 5, Settings: 0, Sources: 0), result);
        Assert.Equal((14, 6, 1, 1), await CountsAsync());
        await using var scope = _services.CreateAsyncScope();
        var after = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var cancellation = TestContext.Current.CancellationToken;
        var toronto = await after.Regions.SingleAsync(region => region.Slug == "toronto", cancellation);
        Assert.Equal(("City of Toronto", 5), (toronto.Name, toronto.Rank));
        Assert.Equal("GTA", (await after.Regions.SingleAsync(region => region.Slug == "gta", cancellation)).Name);
        Assert.Equal("Happenings", (await after.Categories.SingleAsync(category => category.Slug == "events", cancellation)).Name);
        var settings = await after.Settings.SingleAsync(cancellation);
        Assert.Equal((12, 6), (settings.IntervalHours, settings.RetentionMonths));
        var source = await after.Sources.SingleAsync(cancellation);
        Assert.Equal(("Renamed by staff", "\"church\" AND gym", Start.AddDays(-30)), (source.OrganizationName, source.Address, source.CreatedAt));
    }

    // Each kind of row is judged on its own: what is missing is added, whatever else is there.
    [Fact]
    public async Task Given_a_seeded_database_that_lost_its_news_query_source_When_it_is_seeded_Then_only_that_source_is_added()
    {
        await SeedAsync();
        await using (var arrange = _services.CreateAsyncScope())
        {
            var db = arrange.ServiceProvider.GetRequiredService<IAppDbContext>();
            db.Sources.Remove(await db.Sources.SingleAsync(TestContext.Current.CancellationToken));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var result = await SeedAsync();

        Assert.Equal(new SeedDataResult(Regions: 0, Categories: 0, Settings: 0, Sources: 1), result);
    }

    private async Task<SeedDataResult> SeedAsync()
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new SeedDataCommand(), TestContext.Current.CancellationToken);
    }

    private async Task<(int Regions, int Categories, int Settings, int Sources)> CountsAsync()
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var cancellation = TestContext.Current.CancellationToken;
        return (
            await db.Regions.CountAsync(cancellation),
            await db.Categories.CountAsync(cancellation),
            await db.Settings.CountAsync(cancellation),
            await db.Sources.CountAsync(cancellation));
    }
}
