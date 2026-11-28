using System.Diagnostics;
using Keryx.Domain.Collection;

namespace Keryx.Application.Telemetry;

/// <summary>
/// The traces of worker runs (L2-094 AC3). A run is one trace: a root span per run, named by its
/// worker; a collection run adds a span per stage and, under the fetch stage, a span per source
/// fetch. Spans name a run, a stage and a source by id only, never by address, so no URL or query
/// reaches a span. A pickup that finds no run to execute is dropped and exports nothing.
/// </summary>
public static class RunTelemetry
{
    public const string SourceName = "Keryx.Runs";

    public const string RunIdTag = "keryx.run.id";
    public const string StageTag = "keryx.stage";
    public const string SourceIdTag = "keryx.source.id";

    private static readonly ActivitySource Source = new(SourceName);

    /// <summary>The root span of a worker's run, started before the run is known.</summary>
    internal static Activity? StartRun(RunWorker worker) =>
        Source.StartActivity($"{Runs.RunWords.Worker(worker)}.run", ActivityKind.Internal, parentContext: default);

    /// <summary>Names the run the current root span covers.</summary>
    internal static void Covers(Run run) => Activity.Current?.SetTag(RunIdTag, run.PublicId);

    /// <summary>The current root span found no run to execute: it is not exported.</summary>
    internal static void Discard()
    {
        if (Activity.Current is { } span)
        {
            span.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
            span.IsAllDataRequested = false;
        }
    }

    /// <summary>A stage of a collection run, under the run's span.</summary>
    internal static Activity? StartStage(ActivityContext run, PipelineStage stage)
    {
        var word = Stage(stage);
        var span = Source.StartActivity($"collection.{word}", ActivityKind.Internal, run);
        span?.SetTag(StageTag, word);
        return span;
    }

    /// <summary>One source's fetch, under the fetch stage's span.</summary>
    internal static Activity? StartFetch(ActivityContext stage, Domain.Sources.Source source)
    {
        var span = Source.StartActivity("source.fetch", ActivityKind.Client, stage);
        span?.SetTag(SourceIdTag, source.PublicId);
        return span;
    }

    private static string Stage(PipelineStage stage) => stage switch
    {
        PipelineStage.Fetch => "fetch",
        PipelineStage.Parse => "parse",
        PipelineStage.Classify => "classify",
        PipelineStage.Deduplicate => "deduplicate",
        PipelineStage.Store => "store",
        PipelineStage.Publish => "publish",
        _ => throw new ArgumentOutOfRangeException(nameof(stage)),
    };
}
