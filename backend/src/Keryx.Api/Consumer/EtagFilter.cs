using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Keryx.Api.Consumer;

/// <summary>
/// Conditional GET for the consumer API (L2-032). A successful read gets a strong ETag, the
/// SHA-256 of its JSON body, and <c>Cache-Control: private, max-age=60</c>; when the request's
/// <c>If-None-Match</c> holds that ETag (compared weakly, as RFC 9110 says) or <c>*</c>, the
/// answer is <c>304</c> with no body. Because the ETag hashes the content itself, any change gives
/// a new one. A failure carries neither header. A <c>304</c> still runs the query: it saves the
/// product's bandwidth, not Keryx's work.
/// </summary>
public sealed class EtagFilter(IOptions<JsonOptions> json) : IAsyncResultFilter
{
    public const string CacheControl = "private, max-age=60";

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        var request = context.HttpContext.Request;
        if (!HttpMethods.IsGet(request.Method) || context.Result is not ObjectResult { Value: { } value } result
            || result.StatusCode is not (null or StatusCodes.Status200OK))
        {
            await next().ConfigureAwait(false);
            return;
        }

        var body = JsonSerializer.SerializeToUtf8Bytes(value, value.GetType(), json.Value.JsonSerializerOptions);
        var etag = new EntityTagHeaderValue($"\"{Convert.ToBase64String(SHA256.HashData(body))[..43].Replace('+', '-').Replace('/', '_')}\"");
        var headers = context.HttpContext.Response.GetTypedHeaders();
        headers.ETag = etag;
        context.HttpContext.Response.Headers.CacheControl = CacheControl;
        var ifNoneMatch = request.GetTypedHeaders().IfNoneMatch;
        if (ifNoneMatch.Any(candidate => candidate.Equals(EntityTagHeaderValue.Any) || candidate.Compare(etag, useStrongComparison: false)))
        {
            context.Result = new StatusCodeResult(StatusCodes.Status304NotModified);
        }

        await next().ConfigureAwait(false);
    }
}

/// <summary>Marks a consumer controller's reads for conditional GET (L2-032).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
internal sealed class ConditionalGetAttribute() : ServiceFilterAttribute(typeof(EtagFilter));
