using System.Collections.Frozen;
using System.Diagnostics;
using OpenTelemetry;

namespace Keryx.Infrastructure.Telemetry;

/// <summary>
/// Runs on every span before it is exported and keeps only the tags on the allow-list (L2-094):
/// a span says which route template was asked for, how and with what result, and nothing of the
/// path, the query string, a header, a body or a person. A status keeps its code and loses its
/// description, which may quote an exception's message. A tag a later feature adds is dropped
/// until it joins <see cref="AllowedTags"/>.
/// </summary>
internal sealed class TelemetryRedactionProcessor : BaseProcessor<Activity>
{
    public static readonly FrozenSet<string> AllowedTags = FrozenSet.ToFrozenSet(
        ["error.type", "http.request.method", "http.response.status_code", "http.route", "keryx.client.id", "keryx.run.id", "keryx.source.id", "keryx.stage"],
        StringComparer.Ordinal);

    public override void OnEnd(Activity data)
    {
        ArgumentNullException.ThrowIfNull(data);

        // Collected first: removing a tag while enumerating them would skip its neighbour.
        List<string>? dropped = null;
        foreach (var tag in data.EnumerateTagObjects())
        {
            if (!AllowedTags.Contains(tag.Key))
            {
                (dropped ??= []).Add(tag.Key);
            }
        }

        if (dropped is not null)
        {
            foreach (var key in dropped)
            {
                data.SetTag(key, null);
            }
        }

        if (data.StatusDescription is not null)
        {
            data.SetStatus(data.Status);
        }
    }
}
