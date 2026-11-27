using System.Net.Http.Json;
using System.Security;
using System.Text.Json;
using Keryx.Application.Collection;
using Keryx.Domain.Collection;
using Keryx.Domain.Sources;
using Keryx.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Keryx.Api.Tests.Runs;

/// <summary>Collection runs for the tests: a source added, a run made pending as a slot makes it, and the pipeline over it.</summary>
internal static class CollectionRuns
{
    /// <summary>Adds a source and returns its public id.</summary>
    public static async Task<string> AddSourceAsync(ApiFactory factory, SourceKind kind, string address, string name, string region)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
        var source = Source.Add(kind, address, name, region, factory.Time.GetUtcNow());
        db.Sources.Add(source);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return source.PublicId;
    }

    /// <summary>A pending run named by the lease, as a slot makes it, then the pipeline over it; returns the run's id.</summary>
    public static async Task<string> RunAsync(ApiFactory factory)
    {
        string runId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KeryxDbContext>();
            var run = Run.Start(RunWorker.Collection, RunTrigger.Schedule, factory.Time.GetUtcNow());
            db.Runs.Add(run);
            var lease = await db.RunLeases.SingleOrDefaultAsync(TestContext.Current.CancellationToken);
            if (lease is null)
            {
                lease = RunLease.For(RunWorker.Collection, factory.Time.GetUtcNow());
                db.RunLeases.Add(lease);
            }

            lease.Claim(run.PublicId);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            runId = run.PublicId;
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunCollectionCommand(), TestContext.Current.CancellationToken);
        }

        return runId;
    }

    public static async Task<JsonElement> ReadRunAsync(HttpClient client, string runId) =>
        await client.GetFromJsonAsync<JsonElement>(new Uri($"/admin/v1/runs/{runId}", UriKind.Relative), TestContext.Current.CancellationToken);

    /// <summary>An RSS feed of the given items, each a title, a link and a summary.</summary>
    public static string Feed(params (string Title, string Link, string Summary)[] items) =>
        "<rss version=\"2.0\"><channel><title>Feed</title>" +
        string.Concat(items.Select(item =>
            $"<item><title>{SecurityElement.Escape(item.Title)}</title><link>{SecurityElement.Escape(item.Link)}</link><description>{SecurityElement.Escape(item.Summary)}</description></item>")) +
        "</channel></rss>";
}
