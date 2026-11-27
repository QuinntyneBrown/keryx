using System.Diagnostics;
using System.Text.Json;
using Keryx.Application.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Keryx.Api.Errors;

/// <summary>
/// The one writer of every error the API answers with: an exception, a status with no body and a
/// response the framework refuses all become an RFC 9457 ProblemDetails with the <c>code</c> a
/// client branches on and the <c>trace_id</c> a person quotes (L2-031, L2-094).
///
/// An unhandled exception is <c>500</c> <c>internal_error</c> with a generic title. Nothing of
/// the exception reaches the body: no message, stack trace, SQL, path or host name. It is logged.
/// </summary>
internal sealed partial class ProblemDetailsMapper(IProblemDetailsService problems, ILogger<ProblemDetailsMapper> logger)
    : IExceptionHandler
{
    private const string Code = "code";
    private const string TraceId = "trace_id";
    private const string Errors = "errors";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            RequestValidationException refusal => Refused(refusal, refusal.Errors),
            KeryxException refusal => Refused(refusal, errors: null),
            _ => Unhandled(exception),
        };
        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problem })
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Runs for every ProblemDetails, whoever made it. It gives the trace's id as
    /// <c>trace_id</c> in place of the framework's <c>traceId</c>, and a status with a code
    /// every endpoint shares (<c>404</c>, <c>500</c> and above) its code when it has none.
    /// </summary>
    public static void Customize(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        problem.Extensions.Remove("traceId");
        problem.Extensions[TraceId] = Activity.Current?.TraceId.ToHexString() ?? context.HttpContext.TraceIdentifier;
        if (problem.Status == StatusCodes.Status401Unauthorized && context.HttpContext.Items.ContainsKey(Session.StaffTicketStore.Expired))
        {
            problem.Extensions.TryAdd(Code, "session_expired");
        }

        if (problem.Status == StatusCodes.Status403Forbidden && context.HttpContext.Request.Path.StartsWithSegments("/admin/v1"))
        {
            problem.Extensions.TryAdd(Code, "not_staff");
        }

        if (!problem.Extensions.ContainsKey(Code) && CodeOf(problem.Status) is { } code)
        {
            problem.Extensions[Code] = code;
        }
    }

    private static string? CodeOf(int? status) => status switch
    {
        StatusCodes.Status404NotFound => ErrorCodes.NotFound,
        >= StatusCodes.Status500InternalServerError => ErrorCodes.InternalError,
        _ => null,
    };

    private static ProblemDetails Refused(KeryxException refusal, IReadOnlyDictionary<string, string[]>? errors)
    {
        var problem = new ProblemDetails { Status = refusal.Status, Title = refusal.Title };
        problem.Extensions[Code] = refusal.Code;
        if (errors is not null)
        {
            // A field is named as the client sent it: snake_case, like every JSON property.
            problem.Extensions[Errors] = errors.ToDictionary(
                entry => JsonNamingPolicy.SnakeCaseLower.ConvertName(entry.Key),
                entry => entry.Value,
                StringComparer.Ordinal);
        }

        return problem;
    }

    private ProblemDetails Unhandled(Exception exception)
    {
        LogUnhandled(logger, exception);
        return new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Extensions = { [Code] = ErrorCodes.InternalError },
        };
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "An unhandled exception reached the API")]
    private static partial void LogUnhandled(ILogger logger, Exception exception);
}
