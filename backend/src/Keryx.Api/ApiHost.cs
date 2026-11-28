using System.Text.Json;
using Keryx.Api.Consumer;
using Keryx.Api.Errors;
using Keryx.Api.Health;
using Keryx.Api.Session;
using Keryx.Application;
using Keryx.Infrastructure;
using Keryx.Infrastructure.Health;
using Keryx.Infrastructure.Telemetry;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Keryx.Api;

/// <summary>The composition of the API process: its services and its HTTP pipeline.</summary>
public static class ApiHost
{
    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddSourceFetching();
        builder.Services.AddHostedService<SchemaStatusLogger>();
        builder.Services.AddHostedService<ApiUsageFlushService>();

        // Traces, metrics and logs (L2-094). ASP.NET Core's own request logs print the full URL,
        // query string included, so they stay out of every log.
        builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
        builder.Services.AddKeryxTelemetry(
            builder.Configuration,
            KeryxTelemetry.ApiServiceName,
            tracing => tracing.AddAspNetCoreInstrumentation(),
            metrics => metrics.AddAspNetCoreInstrumentation());

        // JSON property names are snake_case, in requests and in answers (L2 conventions).
        builder.Services.AddControllers().AddJsonOptions(options =>
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);

        // Errors are ProblemDetails with a code, from one writer (L2-031).
        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ProblemDetailsMapper.Customize);
        builder.Services.AddExceptionHandler<ProblemDetailsMapper>();

        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddApiKey()
            .AddCookie(options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });
        builder.Services.AddOptions<Keryx.Application.Clients.ConsumerApiOptions>().BindConfiguration(Keryx.Application.Clients.ConsumerApiOptions.Section)
            .Validate(options => Keryx.Application.Clients.ConsumerApiOptions.IsValid(options.PublicBaseUrl), Keryx.Application.Clients.ConsumerApiOptions.MissingPublicBaseUrl)
            .ValidateOnStart();
        builder.Services.AddOptions<SessionLimits>().BindConfiguration("Keryx:Session")
            .Validate(limits => limits.IdleMinutes > 0 && limits.LifetimeHours > 0,
                "Session idle minutes and lifetime hours must be positive.").ValidateOnStart();
        builder.Services.AddSingleton<StaffTicketStore>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<Keryx.Application.Audit.ICurrentStaff, Keryx.Api.Audit.HttpContextCurrentStaff>();
        builder.Services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<StaffTicketStore, TimeProvider, Microsoft.Extensions.Options.IOptions<SessionLimits>>((options, store, clock, limits) =>
            {
                options.SessionStore = store;
                options.TimeProvider = clock;
                options.ExpireTimeSpan = TimeSpan.FromHours(limits.Value.LifetimeHours);
                options.SlidingExpiration = false;
            });
        builder.Services.AddStaffOidc();
        builder.Services.AddAntiforgery(AdminAntiforgery.Configure);
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("Staff", policy => policy
                .AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .RequireClaim("roles", builder.Configuration["Keryx:Oidc:StaffClaim"] ?? "KeryxStaff"))
            .AddApiKeyPolicies()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler, ApiAuthorizationResultHandler>();
        builder.Services.AddSingleton<Keryx.Application.Consumer.ICursorCodec, DataProtectionCursorCodec>();
        builder.Services.AddOptions<RateLimitOptions>().BindConfiguration("Keryx:RateLimits")
            .Validate(limits => limits.ClientPermitsPerMinute > 0 && limits.UnauthenticatedPermitsPerMinute > 0,
                "Rate limits must be positive.").ValidateOnStart();
        builder.Services.AddSingleton<ClientRateLimiter>();
        builder.Services.AddSingleton<EtagFilter>();
        builder.Services.AddConsumerOpenApi();
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);

        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/admin/v1"))
            {
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers.CacheControl = "no-store";
                    return Task.CompletedTask;
                });
            }

            await next(context).ConfigureAwait(false);
        });
        app.UseMiddleware<ApiUsageMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.Use(AdminBodyLimit.EnforceAsync);
        app.UseRouting();
        app.UseAuthentication();
        // A missing route has no resource to authorize. Keep its documented 404 response.
        app.Use(async (context, next) =>
        {
            if (context.GetEndpoint() is null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await next(context).ConfigureAwait(false);
        });
        app.UseMiddleware<ClientRateLimiter>();
        app.UseAuthorization();
        app.Use(AdminAntiforgery.ValidateAsync);
        app.MapGet("/admin/v1/auth/antiforgery", AdminAntiforgery.Issue).RequireAuthorization();
        MapHealth(app);
        app.MapControllers();
        app.MapOpenApi().AllowAnonymous();
        return app;
    }

    /// <summary>
    /// Liveness runs no check, so it never touches the database. Readiness runs the checks
    /// tagged <see cref="HealthTags.Ready"/> and answers <c>503</c> unless all are healthy. Both
    /// answer with the status word alone, as plain text, and need no credential (L2-095).
    /// </summary>
    private static void MapHealth(WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(HealthTags.Ready) })
            .AllowAnonymous();
    }
}
