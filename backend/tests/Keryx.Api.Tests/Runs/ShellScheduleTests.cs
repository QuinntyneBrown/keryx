using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using Keryx.Domain;
using Keryx.Domain.Seeding;
using Keryx.Domain.Sources;
using Keryx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Keryx.Api.Tests.Runs.CollectionRuns;

namespace Keryx.Api.Tests.Runs;

// Acceptance Test
// Traces to: L2-042 AC1, AC3; L2-056 AC1 (the header's schedule)
// Description: The shell's schedule in one read: the next collection, the interval, the zone the
// app formats times in, and how many sources are failing, for the Runs badge.
public sealed class ShellScheduleTests
{
    private static readonly Uri Schedule = new("/admin/v1/overview/schedule", UriKind.Relative);
    private static readonly string[] Keys = ["failing_sources", "interval_hours", "next_run_at", "time_zone"];

    /// <summary>28 November 2026, 09:46 in Toronto: the next 6-hourly run is at 12:00 (17:00 UTC).</summary>
    private static readonly DateTimeOffset NineFortySix = new(2026, 11, 28, 14, 46, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_2_failing_sources_among_others_When_the_schedule_is_read_Then_it_counts_them_with_the_next_run_and_interval()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        var failing = new[]
        {
            await AddSourceAsync(factory, SourceKind.Rss, "https://redeemedrun.ca/feed", "Redeemed Run Club", "toronto"),
            await AddSourceAsync(factory, SourceKind.Rss, "https://covenantcycling.ca/feed", "Covenant Cycling Collective", "toronto"),
        };
        var needsLook = await AddSourceAsync(factory, SourceKind.Rss, "https://templefit.ca/feed", "Temple Fit", "toronto");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
            var now = factory.Time.GetUtcNow();
            foreach (var source in await db.Sources.Where(source => failing.Contains(source.PublicId)).ToListAsync(TestContext.Current.CancellationToken))
            {
                source.RecordFetchFailed(now, "not_found");
            }

            (await db.Sources.SingleAsync(source => source.PublicId == needsLook, TestContext.Current.CancellationToken)).RecordFetchSucceeded(now, 3);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var response = await client.GetAsync(Schedule, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var schedule = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(Keys, schedule.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(2, schedule.GetProperty("failing_sources").GetInt32());
        Assert.Equal(6, schedule.GetProperty("interval_hours").GetInt32());
        Assert.Equal(new DateTimeOffset(2026, 11, 28, 17, 0, 0, TimeSpan.Zero), schedule.GetProperty("next_run_at").GetDateTimeOffset());
        Assert.Equal("America/Toronto", schedule.GetProperty("time_zone").GetString());
    }

    [Fact]
    public async Task Given_no_failing_source_When_the_schedule_is_read_Then_none_is_counted()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        await AddSourceAsync(factory, SourceKind.Rss, "https://redeemedrun.ca/feed", "Redeemed Run Club", "toronto");

        var schedule = await client.GetFromJsonAsync<JsonElement>(Schedule, TestContext.Current.CancellationToken);

        Assert.Equal(0, schedule.GetProperty("failing_sources").GetInt32());
    }

    [Fact]
    public async Task Given_a_12_hour_schedule_When_the_schedule_is_read_Then_its_interval_is_12()
    {
        using var factory = new ApiFactory();
        using var client = await OpenAsync(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
            db.Settings.Remove(await db.Settings.SingleAsync(TestContext.Current.CancellationToken));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            db.Settings.Add(Settings.Create(SeedData.DefaultValues() with { IntervalHours = 12, RetentionMonths = 6 }));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var schedule = await client.GetFromJsonAsync<JsonElement>(Schedule, TestContext.Current.CancellationToken);

        Assert.Equal(12, schedule.GetProperty("interval_hours").GetInt32());
    }

    [Fact]
    public async Task Given_no_staff_session_When_the_schedule_is_read_Then_401()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Schedule, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<HttpClient> OpenAsync(ApiFactory factory)
    {
        factory.Time.SetUtcNow(NineFortySix);
        await StaffClient.SeedAsync(factory);
        return await StaffClient.OpenAsync(factory);
    }
}
