using System.Globalization;
using Keryx.Application.Clients;
using Keryx.Domain.Clients;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Keryx.Api.Consumer;

/// <summary>The consumer API's limits, per deployment (L2-038): <c>Keryx:RateLimits</c>.</summary>
public sealed class RateLimitOptions
{
    /// <summary>Requests a client may send in a minute.</summary>
    public int ClientPermitsPerMinute { get; set; } = 600;

    /// <summary>Requests with no valid key an address may send in a minute.</summary>
    public int UnauthenticatedPermitsPerMinute { get; set; } = 60;
}

/// <summary>
/// Limits /v1 requests (L2-038). An address that has used its requests without a valid key is
/// refused before any key is looked up. Otherwise the request is authenticated (the scheme keeps
/// its result for the policy) and charged to its client's window, or, with no valid key, to its
/// address. A refusal is <c>429</c> <c>rate_limited</c> with <c>Retry-After</c>; a successful
/// response carries <c>RateLimit-Limit</c>, <c>RateLimit-Remaining</c> and <c>RateLimit-Reset</c>.
/// The address is read from the connection; a deployment behind a proxy configures forwarded headers.
/// </summary>
internal sealed class ClientRateLimiter(
    IRateWindowStore windows, TimeProvider time, IOptions<RateLimitOptions> options, IProblemDetailsService problems) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        if (!context.Request.Path.StartsWithSegments("/v1"))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var limits = options.Value;
        var now = time.GetUtcNow();
        var address = $"address:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
        if (windows.IsFull(address, now))
        {
            await RefuseAsync(context, windows.TryTake(address, now, limits.UnauthenticatedPermitsPerMinute)).ConfigureAwait(false);
            return;
        }

        var authenticated = await context.AuthenticateAsync(ApiKeyDefaults.Scheme).ConfigureAwait(false);
        var client = authenticated.Succeeded ? authenticated.Principal.FindFirst(ApiKeyDefaults.ClientClaim)?.Value : null;
        if (client is null)
        {
            var charged = windows.TryTake(address, now, limits.UnauthenticatedPermitsPerMinute);
            if (!charged.Allowed)
            {
                await RefuseAsync(context, charged).ConfigureAwait(false);
                return;
            }

            await next(context).ConfigureAwait(false);
            return;
        }

        var decision = windows.TryTake($"client:{client}", now, limits.ClientPermitsPerMinute);
        if (!decision.Allowed)
        {
            await RefuseAsync(context, decision).ConfigureAwait(false);
            return;
        }

        context.Response.OnStarting(() =>
        {
            if (context.Response.StatusCode is >= 200 and < 300)
            {
                var headers = context.Response.Headers;
                headers["RateLimit-Limit"] = decision.Limit.ToString(CultureInfo.InvariantCulture);
                headers["RateLimit-Remaining"] = decision.Remaining.ToString(CultureInfo.InvariantCulture);
                headers["RateLimit-Reset"] = decision.ResetSeconds.ToString(CultureInfo.InvariantCulture);
            }

            return Task.CompletedTask;
        });
        await next(context).ConfigureAwait(false);
    }

    private async Task RefuseAsync(HttpContext context, RateDecision decision)
    {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter = decision.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        var problem = new ProblemDetails { Status = StatusCodes.Status429TooManyRequests, Title = "Too many requests in this minute." };
        problem.Extensions["code"] = "rate_limited";
        await problems.WriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem }).ConfigureAwait(false);
    }
}
