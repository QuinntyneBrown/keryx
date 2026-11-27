namespace Keryx.Domain;

/// <summary>Toronto's zone, in which the schedule runs: Toronto and the GTA come first.</summary>
public static class TorontoTime
{
    public const string ZoneId = "America/Toronto";

    public static TimeZoneInfo Zone { get; } = TimeZoneInfo.FindSystemTimeZoneById(ZoneId);
}
