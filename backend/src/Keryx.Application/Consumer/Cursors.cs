namespace Keryx.Application.Consumer;

/// <summary>
/// Where a page ended: which list, the filters it was read with, and the last row's sort time and
/// id. A product holds it only as an opaque string (L2-026).
/// </summary>
public sealed record CursorPosition(string List, string Filters, DateTimeOffset SortedAt, long Id);

/// <summary>Turns a position into the opaque cursor a product sends back, and refuses any cursor Keryx did not issue.</summary>
public interface ICursorCodec
{
    string Encode(CursorPosition position);

    /// <summary>The position a cursor holds, or null when Keryx did not issue it.</summary>
    CursorPosition? Decode(string cursor);
}
