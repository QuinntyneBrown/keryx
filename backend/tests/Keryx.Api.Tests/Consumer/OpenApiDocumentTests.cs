using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Keryx.Api.Tests.Consumer;

// Acceptance Test
// Traces to: L2-025 AC1, AC2
// Description: /openapi/v1.json describes every /v1 endpoint, each parameter with its bounds, each
// response body and each error code, and nothing of the admin console.
public sealed class OpenApiDocumentTests
{
    [Fact]
    public async Task Given_no_key_When_the_v1_document_is_read_Then_it_lists_exactly_the_v1_endpoints_and_a_bearer_scheme()
    {
        var document = await DocumentAsync();

        Assert.Equal(["/v1/articles", "/v1/articles/{id}", "/v1/events", "/v1/events/{id}", "/v1/organizations", "/v1/organizations/{id}", "/v1/sources"], document.GetProperty("paths").EnumerateObject().Select(path => path.Name).Order(StringComparer.Ordinal));
        var scheme = document.GetProperty("components").GetProperty("securitySchemes").EnumerateObject().Single().Value;
        Assert.Equal(("http", "bearer"), (scheme.GetProperty("type").GetString(), scheme.GetProperty("scheme").GetString()));
    }

    [Fact]
    public async Task Given_the_list_operation_When_its_parameters_are_read_Then_limit_carries_its_bounds_and_each_filter_its_form()
    {
        var list = (await DocumentAsync()).GetProperty("paths").GetProperty("/v1/articles").GetProperty("get");

        var parameters = list.GetProperty("parameters").EnumerateArray().ToDictionary(p => p.GetProperty("name").GetString()!, p => p.GetProperty("schema"));
        Assert.Equal(["category", "cursor", "limit", "org", "region", "since"], parameters.Keys.Order(StringComparer.Ordinal));
        var limit = parameters["limit"];
        Assert.Equal(("integer", 1, 100, 20), (limit.GetProperty("type").GetString(), limit.GetProperty("minimum").GetInt32(), limit.GetProperty("maximum").GetInt32(), limit.GetProperty("default").GetInt32()));
        Assert.Equal(
            ["events", "faith-wellness", "gyms-studios", "ministry-programs", "nutrition", "races-challenges"],
            parameters["category"].GetProperty("enum").EnumerateArray().Select(value => value.GetString()).Order(StringComparer.Ordinal));
        Assert.Equal("date-time", parameters["since"].GetProperty("format").GetString());
        Assert.Equal("^org_[a-z2-7]{12}$", parameters["org"].GetProperty("pattern").GetString());
    }

    public static TheoryData<string, string, string[]> ErrorCodes => new()
    {
        { "/v1/articles", "400", ["invalid_cursor", "invalid_filter", "invalid_limit"] },
        { "/v1/articles", "401", ["invalid_api_key"] },
        { "/v1/articles", "403", ["insufficient_scope"] },
        { "/v1/articles", "429", ["rate_limited"] },
        { "/v1/articles/{id}", "404", ["article_not_found"] },
        { "/v1/articles/{id}", "401", ["invalid_api_key"] },
        { "/v1/articles/{id}", "403", ["insufficient_scope"] },
        { "/v1/articles/{id}", "429", ["rate_limited"] },
        { "/v1/events", "400", ["invalid_cursor", "invalid_filter", "invalid_limit"] },
        { "/v1/events", "429", ["rate_limited"] },
        { "/v1/events/{id}", "404", ["event_not_found"] },
        { "/v1/events/{id}", "403", ["insufficient_scope"] },
        { "/v1/organizations", "400", ["invalid_cursor", "invalid_filter", "invalid_limit"] },
        { "/v1/organizations/{id}", "404", ["organization_not_found"] },
        { "/v1/organizations/{id}", "401", ["invalid_api_key"] },
        { "/v1/sources", "400", ["invalid_cursor", "invalid_limit"] },
        { "/v1/sources", "403", ["insufficient_scope"] },
    };

    [Fact]
    public async Task Given_the_organizations_list_When_its_parameters_are_read_Then_kind_lists_its_words_and_q_its_length_bounds()
    {
        var list = (await DocumentAsync()).GetProperty("paths").GetProperty("/v1/organizations").GetProperty("get");

        var parameters = list.GetProperty("parameters").EnumerateArray().ToDictionary(p => p.GetProperty("name").GetString()!, p => p.GetProperty("schema"));
        Assert.Equal(["cursor", "kind", "limit", "q", "region"], parameters.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(
            ["business", "church", "club", "gym", "ministry", "network", "other"],
            parameters["kind"].GetProperty("enum").EnumerateArray().Select(value => value.GetString()).Order(StringComparer.Ordinal));
        Assert.Equal((2, 60), (parameters["q"].GetProperty("minLength").GetInt32(), parameters["q"].GetProperty("maxLength").GetInt32()));
    }

    [Fact]
    public async Task Given_the_events_list_When_its_parameters_are_read_Then_from_and_to_are_times_and_limit_is_bounded()
    {
        var list = (await DocumentAsync()).GetProperty("paths").GetProperty("/v1/events").GetProperty("get");

        var parameters = list.GetProperty("parameters").EnumerateArray().ToDictionary(p => p.GetProperty("name").GetString()!, p => p.GetProperty("schema"));
        Assert.Equal(["cursor", "from", "limit", "org", "region", "to"], parameters.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(("date-time", "date-time"), (parameters["from"].GetProperty("format").GetString(), parameters["to"].GetProperty("format").GetString()));
        Assert.Equal(100, parameters["limit"].GetProperty("maximum").GetInt32());
    }

    [Theory]
    [MemberData(nameof(ErrorCodes))]
    public async Task Given_an_operation_When_its_error_responses_are_read_Then_each_status_lists_its_codes_as_a_problem(string path, string status, string[] codes)
    {
        var operation = (await DocumentAsync()).GetProperty("paths").GetProperty(path).GetProperty("get");

        var schema = operation.GetProperty("responses").GetProperty(status).GetProperty("content").GetProperty("application/problem+json").GetProperty("schema");

        Assert.Equal(codes, schema.GetProperty("properties").GetProperty("code").GetProperty("enum").EnumerateArray().Select(code => code.GetString()).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Given_the_operations_When_their_success_bodies_are_read_Then_each_names_its_fields_in_snake_case()
    {
        var document = await DocumentAsync();
        var schemas = document.GetProperty("components").GetProperty("schemas");

        string[] Fields(string path)
        {
            var reference = document.GetProperty("paths").GetProperty(path).GetProperty("get").GetProperty("responses").GetProperty("200")
                .GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString()!;
            return [.. schemas.GetProperty(reference.Split('/')[^1]).GetProperty("properties").EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)];
        }

        Assert.Equal(["data", "next_cursor"], Fields("/v1/articles"));
        Assert.Equal(["data", "next_cursor"], Fields("/v1/events"));
        Assert.Equal(
            ["article_id", "ends_at", "id", "organization", "place_name", "region", "starts_at", "time_zone", "title", "url"],
            Fields("/v1/events/{id}"));
        Assert.Equal(["article_count", "id", "kind", "name", "region", "website_url"], Fields("/v1/organizations/{id}"));
        Assert.Equal(["data", "next_cursor"], Fields("/v1/sources"));
        var sourcesPage = schemas.GetProperty(document.GetProperty("paths").GetProperty("/v1/sources").GetProperty("get").GetProperty("responses").GetProperty("200")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString()!.Split('/')[^1]);
        var item = sourcesPage.GetProperty("properties").GetProperty("data").GetProperty("items").GetProperty("$ref").GetString()!.Split('/')[^1];
        Assert.Equal(["kind", "name", "organization", "region"], schemas.GetProperty(item).GetProperty("properties").EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal(
            ["category", "collected_at", "event_id", "id", "organization", "published_at", "region", "sources", "summary", "tags", "title", "url"],
            Fields("/v1/articles/{id}"));
    }

    private static async Task<JsonElement> DocumentAsync()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }
}
