using System.Threading.Channels;
using Keryx.Application.Abstractions;
using Keryx.Application.Catalogue;
using Keryx.Application.Scoring;
using Keryx.Application.Sources;
using Keryx.Domain.Collection;
using Keryx.Domain.Sources;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Collection;

/// <summary>
/// The six stages of a collection run (L2-007), over the contracts that reach sources (L2-008).
/// Sources are fetched at most <see cref="FetchConcurrency"/> at once; the fetcher itself keeps
/// one request per host, times out each attempt and retries up to 3 attempts (L2-022). Each
/// fetched source streams through a bounded channel to one reader, which parses and classifies
/// its items, records the source's health and advances the run's progress, so memory does not
/// grow with the number of sources (L2-087). A source that fails, or throws, is recorded as
/// failed and the others continue. Each source's items are matched and staged as they are read
/// (deduplicate and store), and the publish stage applies the run's staged changes in one save.
/// </summary>
public sealed class CollectionPipeline(
    IAppDbContext db, ISourceFetcher fetcher, IFeedParser parser, IRelevanceScorer scorer, SourceIssueTracker issues,
    ArticleIngestion ingestion, ArticlePublication publication, TimeProvider time)
{
    /// <summary>How many sources are fetched at once (L2-022).</summary>
    internal const int FetchConcurrency = 8;

    /// <summary>The store stage's reason when it cannot save; generic, so no host, address or error text reaches a reader (L2-007 AC3).</summary>
    internal const string StoreFailed = "The records of this run could not be saved.";

    /// <summary>How many fetched sources may wait for the reader.</summary>
    internal const int ChannelCapacity = 16;

    /// <summary>The kinds read over plain HTTP; the platform kinds and news queries need clients not built yet.</summary>
    /// <summary>The kinds of source a run fetches; the rest are read by other means.</summary>
    internal static readonly SourceKind[] Fetchable = [SourceKind.Rss, SourceKind.Website];

    public async Task<RunCounts> ExecuteAsync(Run run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        // The settings in force when the run starts; a change applies from the next run.
        var settings = await db.Settings.AsNoTracking().SingleAsync(cancellationToken).ConfigureAwait(false);
        var scoring = ScoringSettings.From(settings);
        var timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        var sources = await db.Sources.Where(source => Fetchable.Contains(source.Kind)).OrderBy(source => source.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var places = await db.Regions.AsNoTracking().ToDictionaryAsync(region => region.Slug, region => region.Name, cancellationToken).ConfigureAwait(false);
        // The worker's run before this one: an issue seen there and again here continues its streak.
        long? previousRunId = await db.Runs.AsNoTracking()
            .Where(other => other.Worker == run.Worker && other.Id != run.Id && other.StartedAt <= run.StartedAt)
            .OrderByDescending(other => other.StartedAt).ThenByDescending(other => other.Id)
            .Select(other => (long?)other.Id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        run.BeginExecution(sources.Count);
        foreach (var stage in new[] { PipelineStage.Fetch, PipelineStage.Parse, PipelineStage.Classify, PipelineStage.Deduplicate, PipelineStage.Store })
        {
            run.RecordStage(stage, StageStatus.Running, 0, new Dictionary<string, int>());
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var started = time.GetTimestamp();
        var fetched = Channel.CreateBounded<(Source Source, SourceFetchResult Result)>(new BoundedChannelOptions(ChannelCapacity)
        {
            SingleReader = true,
        });
        using var stopFetching = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var fetching = FetchAllAsync(sources, timeout, fetched.Writer, stopFetching.Token);
        var storeFailed = false;

        int ok = 0, failed = 0, items = 0, relevant = 0, held = 0, offTopic = 0;
        long parseTicks = 0, classifyTicks = 0, storeTicks = 0;
        var counts = new IngestionCounts();
        await foreach (var (source, result) in fetched.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            var at = time.GetUtcNow();
            var parseStarted = time.GetTimestamp();
            // A page that asks not to be indexed yields nothing, and is not a failure (L2-080 AC4).
            var feed = !result.Succeeded || result.NotModified || result.NoIndex ? null : parser.Parse(source.Kind, result.Content);
            parseTicks += time.GetTimestamp() - parseStarted;
            if (!result.Succeeded || (!result.NotModified && !result.NoIndex && feed is null))
            {
                failed++;
                var reason = result.Failure ?? FetchFailureReason.InvalidContent;
                source.RecordFetchFailed(at, SourceTestResult.Words(reason));
                await issues.FailedAsync(run, previousRunId, source, reason, timeout, at, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                ok++;
                var classifyStarted = time.GetTimestamp();
                var sourceHeld = 0;
                var place = places.GetValueOrDefault(source.RegionSlug, string.Empty);
                var classified = new List<(FeedItem, decimal, PublicationStatus)>();
                // An item with no usable link was dropped by the parser; it is counted, not stored (L2-011 AC2).
                counts.Rejected += feed?.Rejected ?? 0;
                foreach (var item in feed?.Items ?? [])
                {
                    items++;
                    var score = scorer.Score(new ScoringItem(item.Title, item.Summary, place), scoring);
                    var status = PublicationPolicy.Classify(score.Value, settings.PublishThreshold);
                    classified.Add((item, score.Value, status));
                    switch (status)
                    {
                        case PublicationStatus.Published:
                            relevant++;
                            break;
                        case PublicationStatus.NeedsReview:
                            held++;
                            sourceHeld++;
                            break;
                        default:
                            offTopic++;
                            break;
                    }
                }

                classifyTicks += time.GetTimestamp() - classifyStarted;
                var storeStarted = time.GetTimestamp();
                await ingestion.StageAsync(run, source, classified, settings.MergeDuplicates, settings.PublishThreshold, counts, cancellationToken).ConfigureAwait(false);
                storeTicks += time.GetTimestamp() - storeStarted;
                source.RecordFetchSucceeded(at, sourceHeld);
                await issues.SucceededAsync(run, previousRunId, source, sourceHeld, settings.PublishThreshold, at, cancellationToken).ConfigureAwait(false);
            }

            run.SourceChecked();
            try
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // The store stage cannot reach the database: stop reading sources (L2-007 AC3).
                storeFailed = true;
                break;
            }
        }

        if (storeFailed)
        {
            await stopFetching.CancelAsync().ConfigureAwait(false);
        }

        await fetching.ConfigureAwait(false);
        var fetchMs = Milliseconds(time.GetTimestamp() - started);
        run.RecordStage(PipelineStage.Fetch, failed > 0 ? StageStatus.DoneWithWarnings : StageStatus.Done, fetchMs,
            new Dictionary<string, int> { ["sources_ok"] = ok, ["sources_failed"] = failed });
        run.RecordStage(PipelineStage.Parse, StageStatus.Done, Milliseconds(parseTicks), new Dictionary<string, int> { ["items"] = items });
        run.RecordStage(PipelineStage.Classify, held > 0 ? StageStatus.DoneWithWarnings : StageStatus.Done, Milliseconds(classifyTicks),
            new Dictionary<string, int> { ["relevant"] = relevant, ["held"] = held, ["off_topic"] = offTopic });
        run.RecordStage(PipelineStage.Deduplicate, StageStatus.Done, 0,
            new Dictionary<string, int> { ["duplicates"] = counts.Duplicates, ["rejected"] = counts.Rejected });
        run.RecordStage(PipelineStage.Store, StageStatus.Done, Milliseconds(storeTicks),
            new Dictionary<string, int> { ["added"] = counts.Added, ["updated"] = counts.Updated, ["unchanged"] = counts.Unchanged });
        if (storeFailed)
        {
            // Nothing from this run is served: its staged records are discarded and publish does not run (L2-007 AC3, AC5).
            DiscardStaged(run);
            run.FailStage(PipelineStage.Store, StoreFailed, time.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new RunCounts { Held = held, OffTopic = offTopic, SourcesOk = ok, SourcesFailed = failed };
        }

        run.RecordStage(PipelineStage.Publish, StageStatus.Running, 0, new Dictionary<string, int>());
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var publishStarted = time.GetTimestamp();
        var published = await publication.PublishAsync(run, cancellationToken).ConfigureAwait(false);
        run.RecordStage(PipelineStage.Publish, StageStatus.Done, Milliseconds(time.GetTimestamp() - publishStarted),
            new Dictionary<string, int> { ["published"] = published });
        return new RunCounts
        {
            Added = counts.Added,
            Updated = counts.Updated,
            Unchanged = counts.Unchanged,
            Duplicates = counts.Duplicates,
            Held = held,
            OffTopic = offTopic,
            Rejected = counts.Rejected,
            SourcesOk = ok,
            SourcesFailed = failed,
        };
    }

    /// <summary>
    /// The run's staged rows are removed: this context staged every one of them, so each is still
    /// tracked; a saved row is deleted and an unsaved one forgotten.
    /// </summary>
    private void DiscardStaged(Run run)
    {
        foreach (var row in db.StagedArticles.Local.Where(row => row.RunId == run.Id).ToList())
        {
            db.StagedArticles.Remove(row);
        }
    }

    /// <summary>
    /// Fetches every source, a few at once. A source that throws is recorded as refused and the
    /// others continue; the channel is completed whatever happens, so the reader always ends.
    /// </summary>
    private async Task FetchAllAsync(
        List<Source> sources, TimeSpan timeout, ChannelWriter<(Source, SourceFetchResult)> writer, CancellationToken cancellationToken)
    {
        try
        {
            await Parallel.ForEachAsync(
                sources.Select(source => (source.Kind, Address: source.Address, Source: source)),
                new ParallelOptions { MaxDegreeOfParallelism = FetchConcurrency, CancellationToken = cancellationToken },
                async (entry, token) =>
                {
                    SourceFetchResult result;
                    try
                    {
                        result = Uri.TryCreate(entry.Address, UriKind.Absolute, out var address)
                            ? await fetcher.FetchAsync(new SourceFetchRequest(entry.Kind, address, timeout), token).ConfigureAwait(false)
                            : SourceFetchResult.Failed(FetchFailureReason.InvalidContent);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException || !token.IsCancellationRequested)
                    {
                        result = SourceFetchResult.Failed(FetchFailureReason.Refused);
                    }

                    await writer.WriteAsync((entry.Source, result), token).ConfigureAwait(false);
                }).ConfigureAwait(false);
            writer.Complete();
        }
        catch (Exception exception)
        {
            writer.Complete(exception);
        }
    }

    private long Milliseconds(long ticks) => (long)time.GetElapsedTime(0, ticks).TotalMilliseconds;
}
