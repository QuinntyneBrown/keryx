using System.Diagnostics.Metrics;

namespace Keryx.Api.Session;

/// <summary>
/// Counts and logs refused access (L2-085 AC3): a <c>401</c>, a <c>403</c>, or a failed sign-in
/// callback. The counter <c>keryx.auth.refused</c> and the log carry the status, a reason and the
/// route template only, never the credential, cookie, key, key prefix, state or token presented.
/// </summary>
public sealed partial class AccessRefusals
{
    public const string MeterName = "Keryx.Api";
    public const string CounterName = "keryx.auth.refused";

    private readonly Counter<long> _refused;
    private readonly ILogger<AccessRefusals> _logger;

    public AccessRefusals(IMeterFactory meters, ILogger<AccessRefusals> logger)
    {
        ArgumentNullException.ThrowIfNull(meters);
        _refused = meters.Create(MeterName).CreateCounter<long>(CounterName, "{request}", "Requests refused: 401, 403 and failed sign-ins.");
        _logger = logger;
    }

    public void Record(int status, string reason, string route)
    {
        _refused.Add(1, new("status", status), new("reason", reason), new("http.route", route));
        Refused(_logger, status, reason, route);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Access refused: {Status} {Reason} on {Route}.")]
    private static partial void Refused(ILogger logger, int status, string reason, string route);
}

/// <summary>
/// Records each response that ends <c>401</c> or <c>403</c>, with its reason worked out from the
/// path and the session, and its route template. It sits around authentication and
/// authorization, so it sees every refusal they make.
/// </summary>
internal sealed class AccessRefusalMiddleware(RequestDelegate next, AccessRefusals refusals)
{
    public async Task InvokeAsync(HttpContext context)
    {
        await next(context).ConfigureAwait(false);
        var status = context.Response.StatusCode;
        if (status is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden)
        {
            refusals.Record(status, ReasonOf(context, status), RouteOf(context));
        }
    }

    internal static string RouteOf(HttpContext context) =>
        (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText?.TrimStart('/') ?? "unmatched";

    private static string ReasonOf(HttpContext context, int status) =>
        (context.Request.Path.StartsWithSegments("/v1"), status) switch
        {
            (true, StatusCodes.Status401Unauthorized) => "invalid_api_key",
            (true, _) => "insufficient_scope",
            (false, StatusCodes.Status401Unauthorized) => context.Items.ContainsKey(StaffTicketStore.Expired) ? "session_expired" : "not_signed_in",
            _ => "not_staff",
        };
}
