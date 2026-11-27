using System.Text.Json;
using Keryx.Api.Errors;
using Keryx.Api.Health;
using Keryx.Application;
using Keryx.Infrastructure;
using Keryx.Infrastructure.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Keryx.Api;

/// <summary>The composition of the API process: its services and its HTTP pipeline.</summary>
public static class ApiHost
{
    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddHostedService<SchemaStatusLogger>();

        // JSON property names are snake_case, in requests and in answers (L2 conventions).
        builder.Services.AddControllers().AddJsonOptions(options =>
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);

        // Errors are ProblemDetails with a code, from one writer (L2-031).
        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ProblemDetailsMapper.Customize);
        builder.Services.AddExceptionHandler<ProblemDetailsMapper>();

        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        MapHealth(app);
        app.MapControllers();
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
