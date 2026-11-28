using System.Diagnostics;
using Keryx.Application.Clients;
using MediatR;
using Microsoft.AspNetCore.Http.Features;

namespace Keryx.Api.Consumer;

/// <summary>
/// Counts each consumer request (`/v1`) by the client its key named, once the response is final:
/// it sits outside the exception handler, so a failure is counted as the 500 it became. A request
/// with no client, such as one with no valid key, is not counted. Only the client, the status,
/// the time taken and when it ended are passed on (L2-037). The request's measurement
/// (<c>http.server.request.duration</c>) and its span are tagged with the client's public id, so
/// requests, latency and errors are seen by route template and client (L2-094 AC4).
/// </summary>
internal sealed class ApiUsageMiddleware(RequestDelegate next, TimeProvider time)
{
    internal const string ClientIdTag = "keryx.client.id";

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

        context.Features.Get<IHttpMetricsTagsFeature>()?.Tags.Add(new(ClientIdTag, clientId));
        Activity.Current?.SetTag(ClientIdTag, clientId);

        var sender = context.RequestServices.GetRequiredService<ISender>();
        await sender.Send(new RecordApiUsage(clientId, context.Response.StatusCode, time.GetElapsedTime(started), time.GetUtcNow()), CancellationToken.None)
            .ConfigureAwait(false);
    }
}
