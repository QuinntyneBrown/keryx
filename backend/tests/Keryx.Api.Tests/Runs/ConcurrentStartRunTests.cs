using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Fakes;
using Keryx.Api.Tests.Session;
using Keryx.Application.Abstractions;
using Keryx.Domain.Collection;
using Keryx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Keryx.Api.Tests.Runs;

// Acceptance Test
// Traces to: L2-006 AC3
// Description: Two starts at the same moment make one run: the request that loses the race for
// the lease answers run_in_progress with the winner's run, and saves nothing of its own.
public sealed class ConcurrentStartRunTests
{
    [Fact]
    public async Task Given_another_start_claimed_the_lease_first_When_this_one_saves_Then_409_with_the_winners_run_and_one_run_exists()
    {
        using var factory = new Factory();
        await StaffClient.SeedAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);
        // The worker has looked at the schedule: the lease exists, unclaimed.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
            db.RunLeases.Add(RunLease.For(RunWorker.Collection, factory.Time.GetUtcNow().AddHours(-1)));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        string? winner = null;
        factory.Fault.LeaseRace = async () =>
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
            var run = Run.Start(RunWorker.Collection, RunTrigger.Manual, factory.Time.GetUtcNow());
            db.Runs.Add(run);
            (await db.RunLeases.SingleAsync(TestContext.Current.CancellationToken)).Claim(run.PublicId);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            winner = run.PublicId;
        };

        using var response = await client.PostAsync(new Uri("/admin/v1/runs", UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("run_in_progress", problem.GetProperty("code").GetString());
        Assert.NotNull(winner);
        Assert.Equal(winner, problem.GetProperty("run_id").GetString());
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
            Assert.Equal([winner], await db.Runs.Select(run => run.PublicId).ToListAsync(TestContext.Current.CancellationToken));
        }
    }

    private sealed class Factory : ApiFactory
    {
        public SaveFault.Switch Fault { get; } = new();

        protected override void ConfigureServices(IServiceCollection services)
        {
            services.RemoveAll<IAppDbContext>();
            services.AddScoped<IAppDbContext>(provider => new SaveFault(provider.GetRequiredService<KeryxDbContext>(), Fault));
        }
    }
}
