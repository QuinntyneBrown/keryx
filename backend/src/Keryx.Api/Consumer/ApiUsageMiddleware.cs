using Keryx.Application.Clients;
using MediatR;

namespace Keryx.Api.Consumer;

/// <summary>
/// Counts each consumer request (`/v1`) by the client its key named, once the response is final:
/// it sits outside the exception handler, so a failure is counted as the 500 it became. A request
/// with no client, such as one with no valid key, is not counted. Only the client, the status,
/// the time taken and when it ended are passed on (L2-037).
/// </summary>
internal sealed class ApiUsageMiddleware(RequestDelegate next, TimeProvider time)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/v1"))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var started = time.GetTimestamp();
        await next(context).ConfigureAwait(false);
        var clientId = context.User.FindFirst(ApiKeyDefaults.ClientClaim)?.Value;
        if (clientId is null)
        {
            return;
        }

        var sender = context.RequestServices.GetRequiredService<ISender>();
        await sender.Send(new RecordApiUsage(clientId, context.Response.StatusCode, time.GetElapsedTime(started), time.GetUtcNow()), CancellationToken.None)
            .ConfigureAwait(false);
    }
}
