// Acceptance Test
// Traces to: L2-031, L2-083, L2-094
// Description: Every error is a ProblemDetails with a code and the trace's id, and tells nothing of the system

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Probes;
using Microsoft.Extensions.DependencyInjection;

namespace Keryx.Api.Tests.Errors;

public sealed class ProblemDetailsAcceptanceTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string TraceId = "4bf92f3577b34da6a3ce929d0e0e4736";

    private static readonly string[] ProblemKeys = ["code", "status", "title", "trace_id", "type"];

    // L2-031 AC3 and AC1.
    [Fact]
    public async Task Given_a_path_that_does_not_exist_When_it_is_requested_Then_the_answer_is_404_not_found_as_a_problem()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/v1/no-such-thing", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadAsync(response);
        Assert.Equal(ProblemKeys, Keys(problem));
        Assert.Equal("not_found", problem.GetProperty("code").GetString());
        Assert.Equal(404, problem.GetProperty("status").GetInt32());
        Assert.Matches("^[0-9a-f]{32}$", problem.GetProperty("trace_id").GetString());
    }

    // L2-031 AC2: nothing of the failure reaches the body.
    [Fact]
    public async Task Given_an_unhandled_exception_When_it_is_returned_Then_the_answer_is_500_internal_error_and_says_nothing_more()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/test/probe/throw", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var problem = JsonDocument.Parse(body).RootElement;
        Assert.Equal(ProblemKeys, Keys(problem));
        Assert.Equal("internal_error", problem.GetProperty("code").GetString());
        Assert.Equal(500, problem.GetProperty("status").GetInt32());
        foreach (var leak in new[] { "db.internal", "hunter2", "/srv/keryx", "InvalidOperationException", "ProbeController" })
        {
            Assert.DoesNotContain(leak, body, StringComparison.OrdinalIgnoreCase);
        }
    }

    // L2-094 AC5: the reference a person quotes is the trace's own id.
    [Fact]
    public async Task Given_a_request_inside_a_trace_When_it_fails_Then_trace_id_is_that_trace()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/v1/no-such-thing", UriKind.Relative));
        request.Headers.Add("traceparent", $"00-{TraceId}-00f067aa0ba902b7-01");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        var problem = await ReadAsync(response);
        Assert.Equal(TraceId, problem.GetProperty("trace_id").GetString());
    }

    // L2-083 AC1: a validator runs before the handler, and a refused request changes nothing.
    [Theory]
    [InlineData("", "'Display Name' must not be empty.")]
    [InlineData("six ch", "The length of 'Display Name' must be 5 characters or fewer. You entered 6 characters.")]
    public async Task Given_a_body_a_validator_refuses_When_it_is_sent_Then_the_answer_is_400_validation_failed_naming_the_field(string displayName, string message)
    {
        using var client = factory.CreateClient();
        var handledBefore = factory.Services.GetRequiredService<ProbeCounter>().Handled;

        using var response = await client.PostAsJsonAsync(new Uri("/test/probe", UriKind.Relative), new { display_name = displayName }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadAsync(response);
        Assert.Equal(["code", "errors", "status", "title", "trace_id", "type"], Keys(problem));
        Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
        var errors = problem.GetProperty("errors");
        Assert.Equal(["display_name"], Keys(errors));
        Assert.Equal([message], errors.GetProperty("display_name").EnumerateArray().Select(entry => entry.GetString()));
        Assert.Equal(handledBefore, factory.Services.GetRequiredService<ProbeCounter>().Handled);
    }

    // JSON property names are snake_case, in and out (L2 conventions).
    [Fact]
    public async Task Given_a_valid_body_When_it_is_sent_Then_the_handler_runs_once_and_the_answer_is_snake_case()
    {
        using var client = factory.CreateClient();
        var handledBefore = factory.Services.GetRequiredService<ProbeCounter>().Handled;

        using var response = await client.PostAsJsonAsync(new Uri("/test/probe", UriKind.Relative), new { display_name = "Keryx" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reply = await ReadAsync(response);
        Assert.Equal(["display_name", "handled_count"], Keys(reply));
        Assert.Equal("Keryx", reply.GetProperty("display_name").GetString());
        Assert.Equal(handledBefore + 1, factory.Services.GetRequiredService<ProbeCounter>().Handled);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;

    private static string[] Keys(JsonElement element) =>
        [.. element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)];
}
