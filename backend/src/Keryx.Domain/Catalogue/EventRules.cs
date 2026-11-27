using System.Globalization;
using System.Text.RegularExpressions;

namespace Keryx.Domain.Catalogue;

/// <summary>
/// When an event starts: an instant in UTC and the IANA zone it is shown in (L2-016, L2-018). A
/// published time with an offset is converted as given; a local time takes the region's zone. A
/// local time in the spring gap moves forward by the gap, and one in the autumn overlap is its
/// first occurrence, as the collection schedule reads the clock.
/// </summary>
public sealed partial record EventTime(DateTimeOffset StartsAtUtc, string TimeZone)
{
    public static EventTime? From(string? published, string timeZone)
    {
        if (string.IsNullOrWhiteSpace(published))
        {
            return null;
        }

        var text = published.Trim();
        if (Zoned().IsMatch(text))
        {
            return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var zoned)
                ? new EventTime(zoned.ToUniversalTime(), timeZone)
                : null;
        }

        if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return null;
        }

        var local = DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        // A time in the spring gap has the standard offset, which moves it forward by the gap.
        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        return new EventTime(new DateTimeOffset(local.Ticks - offset.Ticks, TimeSpan.Zero), timeZone);
    }

    [GeneratedRegex(@"(?:Z|[+-]\d{2}:?\d{2})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Zoned();
}

/// <summary>
/// Whether two events of one organization are the same event (L2-010 AC4): they start within an
/// hour of each other, inclusive, at the same place, ignoring case, punctuation and whitespace.
/// </summary>
public static class EventMatch
{
    public static readonly TimeSpan Window = TimeSpan.FromHours(1);

    public static bool Same(DateTimeOffset start, string place, DateTimeOffset otherStart, string otherPlace) =>
        (start - otherStart).Duration() <= Window && PlaceKey(place) == PlaceKey(otherPlace);

    /// <summary>A place's match key.</summary>
    public static string PlaceKey(string place) => string.Join(' ', HeadlineSimilarity.Words(place));
}
