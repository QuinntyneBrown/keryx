using System.Diagnostics.Metrics;
using Keryx.Domain.Collection;
using Keryx.Domain.Sources;

namespace Keryx.Application.Telemetry;

/// <summary>
/// The measurements of collection runs (L2-094 AC3), on the meter <c>Keryx.Runs</c>: how long a
/// run took, by outcome; the items it added, updated and left unchanged; the sources that failed;
/// and how long each source's fetch took, named by the source's id. Durations are in seconds, as
/// the OpenTelemetry conventions measure them.
/// </summary>
public sealed class RunMetrics
{
    public const string MeterName = "Keryx.Runs";

    private readonly Histogram<double> _runDuration;
    private readonly Counter<long> _items;
    private readonly Counter<long> _sourcesFailed;
    private readonly Histogram<double> _fetchDuration;

    public RunMetrics(IMeterFactory meters)
    {
        ArgumentNullException.ThrowIfNull(meters);
        var meter = meters.Create(MeterName);
        _runDuration = meter.CreateHistogram<double>("keryx.run.duration", "s", "How long a collection run took.");
        _items = meter.CreateCounter<long>("keryx.run.items", "{item}", "Items a collection run added, updated or left unchanged.");
        _sourcesFailed = meter.CreateCounter<long>("keryx.run.sources_failed", "{source}", "Sources a collection run could not read.");
        _fetchDuration = meter.CreateHistogram<double>("keryx.fetch.duration", "s", "How long one source's fetch took.");
    }

    /// <summary>A finished run: its duration and its counts.</summary>
    internal void Finished(Run run)
    {
        if (run.FinishedAt is { } finished)
        {
            _runDuration.Record((finished - run.StartedAt).TotalSeconds, new KeyValuePair<string, object?>("keryx.run.outcome", Runs.RunWords.Outcome(run.Outcome)));
        }

        _items.Add(run.Counts.Added, new KeyValuePair<string, object?>("result", "added"));
        _items.Add(run.Counts.Updated, new KeyValuePair<string, object?>("result", "updated"));
        _items.Add(run.Counts.Unchanged, new KeyValuePair<string, object?>("result", "unchanged"));
        _sourcesFailed.Add(run.Counts.SourcesFailed);
    }

    /// <summary>One source's fetch, whatever its result.</summary>
    internal void Fetched(Source source, TimeSpan took) =>
        _fetchDuration.Record(took.TotalSeconds, new KeyValuePair<string, object?>(RunTelemetry.SourceIdTag, source.PublicId));
}
