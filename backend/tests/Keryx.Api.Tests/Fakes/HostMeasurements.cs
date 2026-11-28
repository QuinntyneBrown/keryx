using System.Diagnostics.Metrics;

namespace Keryx.Api.Tests.Fakes;

/// <summary>
/// The measurements one host records on the named instruments. OpenTelemetry subscribes to a
/// meter by name, so an exporter also hears the other hosts this process runs at once; this
/// listener hears only the meters made by the host's own <see cref="IMeterFactory"/>.
/// </summary>
public sealed class HostMeasurements : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly List<Measured> _measured = [];

    public HostMeasurements(IMeterFactory meters, params string[] instruments)
    {
        _listener.InstrumentPublished = (instrument, listening) =>
        {
            if (instrument.Meter.Scope == meters && instruments.Contains(instrument.Name))
            {
                listening.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Add(instrument, value, tags));
        _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Add(instrument, value, tags));
        _listener.Start();
    }

    public sealed record Measured(string Instrument, string? Unit, double Value, IReadOnlyDictionary<string, string> Tags);

    public IReadOnlyList<Measured> Of(string instrument)
    {
        lock (_measured)
        {
            return [.. _measured.Where(measured => measured.Instrument == instrument)];
        }
    }

    public void Dispose() => _listener.Dispose();

    private void Add(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var named = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            named[tag.Key] = $"{tag.Value}";
        }

        lock (_measured)
        {
            _measured.Add(new Measured(instrument.Name, instrument.Unit, value, named));
        }
    }
}
