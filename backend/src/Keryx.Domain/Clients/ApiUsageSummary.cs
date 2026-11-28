namespace Keryx.Domain.Clients;

/// <summary>Figures read from usage counts (L2-037).</summary>
public static class ApiUsageSummary
{
    /// <summary>Errors over requests, from 0 to 1; 0 when there were no requests.</summary>
    public static double ErrorRate(int requests, int errors) => requests == 0 ? 0 : (double)errors / requests;
}
