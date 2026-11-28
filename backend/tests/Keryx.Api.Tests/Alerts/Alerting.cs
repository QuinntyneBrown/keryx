using Keryx.Domain;
using Keryx.Domain.Seeding;
using Keryx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Keryx.Api.Tests.Alerts;

/// <summary>Alert settings for the tests, saved as staff would save them.</summary>
internal static class Alerting
{
    public const string Address = "ops@keryx.ca";

    /// <summary>The seeded settings with the alert address set, changed further as a test says.</summary>
    public static async Task ConfigureAsync(ApiFactory factory, Func<SettingsValues, SettingsValues>? change = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
        var settings = await db.Settings.SingleAsync(TestContext.Current.CancellationToken);
        var values = SeedData.DefaultValues() with { AlertEmail = Address };
        settings.Update(change is null ? values : change(values));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
