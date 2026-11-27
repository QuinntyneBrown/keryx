using Microsoft.AspNetCore.Http.Features;

namespace Keryx.Api.Errors;

/// <summary>
/// No admin request body may be larger than 64 KB (L2-083 AC2). A declared length over the
/// limit is refused with <c>413</c> before the body is read; for a body of unknown length the
/// server's own limit is lowered to the same size, so reading past it fails with <c>413</c>.
/// </summary>
internal static class AdminBodyLimit
{
    public const long Bytes = 64 * 1024;

    public static async Task EnforceAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Request.Path.StartsWithSegments("/admin"))
        {
            if (context.Request.ContentLength > Bytes)
            {
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                return;
            }

            if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
            {
                limit.MaxRequestBodySize = Bytes;
            }
        }

        await next(context).ConfigureAwait(false);
    }
}
