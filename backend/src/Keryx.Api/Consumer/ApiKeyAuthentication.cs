using System.Security.Claims;
using System.Text.Encodings.Web;
using Keryx.Application.Clients;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Keryx.Api.Consumer;

/// <summary>The consumer API's scheme and the claims it gives a client (L2-033, L2-034).</summary>
public static class ApiKeyDefaults
{
    public const string Scheme = "ApiKey";
    public const string ScopeClaim = "scope";
    public const string ClientClaim = "client_id";

    /// <summary>The policies of the consumer API: one per scope, and any client.</summary>
    public const string ArticlesRead = "articles:read";
    public const string EventsRead = "events:read";
    public const string SourcesRead = "sources:read";
    public const string AnyClient = "api-client";
}

/// <summary>
/// The <c>ApiKey</c> scheme: it reads only the <c>Authorization</c> header, never the query
/// string, and sends one request to find the key's client. Every refusal is the same <c>401</c>
/// with <c>WWW-Authenticate: Bearer</c> and the code <c>invalid_api_key</c> (L2-033 AC1).
/// </summary>
internal sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISender sender, IProblemDetailsService problems)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string Bearer = "Bearer ";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? header = Request.Headers.Authorization;
        if (header is null || !header.StartsWith(Bearer, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var client = await sender.Send(new AuthenticateApiClient(header[Bearer.Length..].Trim()), Context.RequestAborted).ConfigureAwait(false);
        if (client is null)
        {
            return AuthenticateResult.NoResult();
        }

        var identity = new ClaimsIdentity(
            [new Claim(ApiKeyDefaults.ClientClaim, client.Id), .. client.Scopes.Select(scope => new Claim(ApiKeyDefaults.ScopeClaim, scope))],
            ApiKeyDefaults.Scheme);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), ApiKeyDefaults.Scheme));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = "Bearer";
        var problem = new ProblemDetails { Status = StatusCodes.Status401Unauthorized, Title = "A valid API key is required." };
        problem.Extensions["code"] = "invalid_api_key";
        await problems.WriteAsync(new ProblemDetailsContext { HttpContext = Context, ProblemDetails = problem }).ConfigureAwait(false);
    }
}

/// <summary>
/// Answers a client that lacks an endpoint's scope with <c>403</c> <c>insufficient_scope</c> and
/// the scope it needs in <c>required_scope</c> (L2-034 AC1). Every other result is the default's.
/// </summary>
internal sealed class ApiAuthorizationResultHandler(IProblemDetailsService problems) : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler fallback = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(authorizeResult);
        var scope = authorizeResult.Forbidden
            ? authorizeResult.AuthorizationFailure?.FailedRequirements.OfType<ClaimsAuthorizationRequirement>()
                .FirstOrDefault(requirement => requirement.ClaimType == ApiKeyDefaults.ScopeClaim)?.AllowedValues?.FirstOrDefault()
            : null;
        if (scope is null)
        {
            await fallback.HandleAsync(next, context, policy, authorizeResult).ConfigureAwait(false);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        var problem = new ProblemDetails { Status = StatusCodes.Status403Forbidden, Title = "The API key lacks the scope this endpoint needs." };
        problem.Extensions["code"] = "insufficient_scope";
        problem.Extensions["required_scope"] = scope;
        await problems.WriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem }).ConfigureAwait(false);
    }
}

/// <summary>The consumer API's scheme and policies, registered in the host.</summary>
internal static class ApiKeyRegistration
{
    public static AuthenticationBuilder AddApiKey(this AuthenticationBuilder builder) =>
        builder.AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyDefaults.Scheme, configureOptions: null);

    public static AuthorizationBuilder AddApiKeyPolicies(this AuthorizationBuilder builder)
    {
        foreach (var scope in new[] { ApiKeyDefaults.ArticlesRead, ApiKeyDefaults.EventsRead, ApiKeyDefaults.SourcesRead })
        {
            builder.AddPolicy(scope, policy => policy.AddAuthenticationSchemes(ApiKeyDefaults.Scheme)
                .RequireAuthenticatedUser().RequireClaim(ApiKeyDefaults.ScopeClaim, scope));
        }

        return builder.AddPolicy(ApiKeyDefaults.AnyClient, policy => policy.AddAuthenticationSchemes(ApiKeyDefaults.Scheme).RequireAuthenticatedUser());
    }
}
