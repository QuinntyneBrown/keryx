using Keryx.Api.Tests.Session;
using Keryx.Application.Collection;
using Keryx.Domain;
using Keryx.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Keryx.Api.Tests.Runs;

// Acceptance Test
// Traces to: L2-005 AC2, AC3
// Description: The next run time is the next slot of the interval in Settings, in UTC with zone
// America/Toronto. The overview and /v1/status serve it when they are built.
public sealed class NextRunTests
{
    /// <summary>28 November 2026, 09:46 in Toronto.</summary>
    private static readonly DateTimeOffset NineFortySix = new(2026, 11, 28, 14, 46, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_6_hours_When_the_next_run_is_read_at_09_46_Then_it_is_12_00_in_utc_with_the_toronto_zone()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        factory.Time.SetUtcNow(NineFortySix);

        var next = await ReadAsync(factory);

        Assert.Equal(new DateTimeOffset(2026, 11, 28, 17, 0, 0, TimeSpan.Zero), next.NextRunAt);
        Assert.Equal(TimeSpan.Zero, next.NextRunAt.Offset);
        Assert.Equal("America/Toronto", next.TimeZone);
    }

    [Fact]
    public async Task Given_the_interval_is_saved_as_3_hours_at_09_46_When_the_next_runs_are_read_Then_they_are_12_00_and_15_00()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        factory.Time.SetUtcNow(NineFortySix);
        await SetIntervalAsync(factory, 3);

        Assert.Equal(new DateTimeOffset(2026, 11, 28, 17, 0, 0, TimeSpan.Zero), (await ReadAsync(factory)).NextRunAt);
        factory.Time.SetUtcNow(new DateTimeOffset(2026, 11, 28, 17, 0, 0, TimeSpan.Zero));
        Assert.Equal(new DateTimeOffset(2026, 11, 28, 20, 0, 0, TimeSpan.Zero), (await ReadAsync(factory)).NextRunAt);
    }

    private static async Task<NextRunDto> ReadAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetNextRun(), TestContext.Current.CancellationToken);
    }

    /// <summary>What saving Settings will do (journey 18): change the interval.</summary>
    private static async Task SetIntervalAsync(ApiFactory factory, int hours)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
        var settings = await db.Settings.SingleAsync(TestContext.Current.CancellationToken);
        db.Entry(settings).Property(nameof(Settings.IntervalHours)).CurrentValue = hours;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
