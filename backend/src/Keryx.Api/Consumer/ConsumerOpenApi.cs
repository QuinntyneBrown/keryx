using System.Text.Json.Nodes;
using Keryx.Application.Consumer;
using Keryx.Domain.Catalogue;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Keryx.Api.Consumer;

/// <summary>An error an action refuses with: its status and the <c>code</c> a product branches on (L2-025, L2-031).</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
internal sealed class ProducesErrorCodeAttribute(int status, string code) : Attribute
{
    public int Status { get; } = status;

    public string Code { get; } = code;
}

/// <summary>
/// The v1 OpenAPI document at <c>/openapi/v1.json</c> (L2-025): only <c>/v1</c> endpoints, the
/// bearer key they take, each query parameter's bounds and each error code. It is served without
/// a key, so a product can read the contract before it is given one.
/// </summary>
internal static class ConsumerOpenApi
{
    public const string Document = "v1";
    public const string Scheme = "ApiKey";

    // Every /v1 operation can refuse these, whatever its own errors are (L2-033, L2-038).
    private static readonly (int Status, string Code)[] Common =
    [
        (StatusCodes.Status401Unauthorized, "invalid_api_key"),
        (StatusCodes.Status403Forbidden, "insufficient_scope"),
        (StatusCodes.Status429TooManyRequests, "rate_limited"),
    ];

    public static IServiceCollection AddConsumerOpenApi(this IServiceCollection services) =>
        services.AddOpenApi(Document, options =>
        {
            options.ShouldInclude = description => description.RelativePath?.StartsWith("v1/", StringComparison.Ordinal) == true;
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = "Keryx consumer API";
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal)
                {
                    [Scheme] = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        Description = "An API key from the admin console, sent as Authorization: Bearer kx_live_…",
                    },
                };
                document.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(Scheme, document)] = [] }];
                return Task.CompletedTask;
            });
            options.AddOperationTransformer((operation, context, _) =>
            {
                foreach (var parameter in operation.Parameters?.OfType<OpenApiParameter>() ?? [])
                {
                    parameter.Schema = ParameterSchema(parameter.Name) ?? parameter.Schema;
                }

                var own = context.Description.ActionDescriptor.EndpointMetadata.OfType<ProducesErrorCodeAttribute>().Select(error => (error.Status, error.Code));
                operation.Responses ??= [];
                foreach (var status in own.Concat(Common).GroupBy(error => error.Status, error => error.Code))
                {
                    operation.Responses[status.Key.ToString(System.Globalization.CultureInfo.InvariantCulture)] = Problem(status.Key, [.. status]);
                }

                return Task.CompletedTask;
            });
        });

    // The bounds the validators enforce, written once for every list that takes the parameter.
    private static OpenApiSchema? ParameterSchema(string? name) => name switch
    {
        "limit" => new OpenApiSchema
        {
            Type = JsonSchemaType.Integer,
            Minimum = "1",
            Maximum = PageLimit.Max.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Default = JsonValue.Create(PageLimit.Default),
        },
        "cursor" => new OpenApiSchema { Type = JsonSchemaType.String, Description = "The next_cursor of the previous page, read with the same filters." },
        "category" => new OpenApiSchema { Type = JsonSchemaType.String, Enum = [.. ArticleCategory.Slugs.Select(slug => (JsonNode)JsonValue.Create(slug))] },
        "kind" => new OpenApiSchema { Type = JsonSchemaType.String, Enum = [.. ListOrganizations.Kinds.Select(kind => (JsonNode)JsonValue.Create(kind))] },
        "q" => new OpenApiSchema { Type = JsonSchemaType.String, MinLength = ListOrganizations.QMin, MaxLength = ListOrganizations.QMax, Description = "The start of a name; case and a leading \"The\" are ignored." },
        "region" => new OpenApiSchema { Type = JsonSchemaType.String, Description = "A region's slug, such as toronto." },
        "org" => new OpenApiSchema { Type = JsonSchemaType.String, Pattern = "^org_[a-z2-7]{12}$" },
        "since" or "from" or "to" => new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time", Description = "ISO 8601 with an offset, such as 2026-11-26T12:00:00Z." },
        _ => null,
    };

    private static OpenApiResponse Problem(int status, string[] codes) => new()
    {
        Description = $"A problem whose code is one of: {string.Join(", ", codes)}.",
        Content = new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal)
        {
            ["application/problem+json"] = new()
            {
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Required = new HashSet<string>(StringComparer.Ordinal) { "status", "code" },
                    Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
                    {
                        ["type"] = new OpenApiSchema { Type = JsonSchemaType.String },
                        ["title"] = new OpenApiSchema { Type = JsonSchemaType.String },
                        ["status"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Enum = [JsonValue.Create(status)] },
                        ["code"] = new OpenApiSchema { Type = JsonSchemaType.String, Enum = [.. codes.Select(code => (JsonNode)JsonValue.Create(code))] },
                        ["trace_id"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    },
                },
            },
        },
    };
}
