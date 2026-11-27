using Microsoft.AspNetCore.Antiforgery;

namespace Keryx.Api.Session;

internal static class AdminAntiforgery
{
    public static void Configure(AntiforgeryOptions options)
    {
        options.HeaderName = "X-XSRF-TOKEN";
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.SuppressReadingTokenFromFormBody = true;
    }

    public static async Task ValidateAsync(HttpContext context, RequestDelegate next)
    {
        var method = context.Request.Method;
        if (context.Request.Path.StartsWithSegments("/admin/v1") &&
            (HttpMethods.IsPost(method) || HttpMethods.IsPut(method) ||
             HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method)))
        {
            var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
            if (!await antiforgery.IsRequestValidAsync(context).ConfigureAwait(false))
            {
                await Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                    title: "The request could not be verified.",
                    extensions: new Dictionary<string, object?> { ["code"] = "antiforgery_failed" })
                    .ExecuteAsync(context).ConfigureAwait(false);
                return;
            }
        }

        await next(context).ConfigureAwait(false);
    }

    public static IResult Issue(HttpContext context, IAntiforgery antiforgery) =>
        Results.Ok(new { request_token = antiforgery.GetAndStoreTokens(context).RequestToken });
}
