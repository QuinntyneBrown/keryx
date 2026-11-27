using System.Text.Json;
using Keryx.Api.Errors;
using Keryx.Application;

namespace Keryx.Api;

/// <summary>The composition of the API process: its services and its HTTP pipeline.</summary>
public static class ApiHost
{
    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddApplication();

        // JSON property names are snake_case, in requests and in answers (L2 conventions).
        builder.Services.AddControllers().AddJsonOptions(options =>
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);

        // Errors are ProblemDetails with a code, from one writer (L2-031).
        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ProblemDetailsMapper.Customize);
        builder.Services.AddExceptionHandler<ProblemDetailsMapper>();

        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.MapControllers();
        return app;
    }
}
