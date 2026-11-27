using System.Globalization;

namespace Keryx.Application.Consumer;

/// <summary>
/// Where a page ended: which list, the filters it was read with, the last row's sort value and
/// its id. A product holds it only as an opaque string (L2-026).
/// </summary>
public sealed record CursorPosition(string List, string Filters, string Sort, long Id)
{
    /// <summary>A position in a list sorted by a time, which it keeps to the tick in UTC.</summary>
    public static CursorPosition At(string list, string filters, DateTimeOffset sortedAt, long id) =>
        new(list, filters, sortedAt.UtcTicks.ToString(CultureInfo.InvariantCulture), id);

    /// <summary>The time a position in a time-sorted list holds, or null when it holds none.</summary>
    public DateTimeOffset? Time =>
        long.TryParse(Sort, NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) && ticks <= DateTimeOffset.MaxValue.UtcTicks
            ? new DateTimeOffset(ticks, TimeSpan.Zero)
            : null;
}

/// <summary>Turns a position into the opaque cursor a product sends back, and refuses any cursor Keryx did not issue.</summary>
public interface ICursorCodec
{
    string Encode(CursorPosition position);

    /// <summary>The position a cursor holds, or null when Keryx did not issue it.</summary>
    CursorPosition? Decode(string cursor);
}
