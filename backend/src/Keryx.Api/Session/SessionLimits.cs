namespace Keryx.Api.Session;

internal sealed class SessionLimits
{
    public int IdleMinutes { get; set; } = 60;
    public int LifetimeHours { get; set; } = 12;
}
