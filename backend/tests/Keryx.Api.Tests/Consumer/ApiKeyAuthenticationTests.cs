using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using static Keryx.Api.Tests.Consumer.ConsumerApi;

namespace Keryx.Api.Tests.Consumer;

// Acceptance Test
// Traces to: L2-033 AC1-AC3, AC5; L2-034 AC1 (articles); L2-036 AC1 (the next request)
// Description: Every /v1 request carries an active API key as a Bearer token. Anything else is
// refused with the same 401; a key lacking the endpoint's scope gets 403 naming the scope.
public sealed class ApiKeyAuthenticationTests
{
    /// <summary>An article id no article has: an authenticated request gets 404, not 401.</summary>
    private static readonly Uri Article = new("/v1/articles/art_none0000000000000000", UriKind.Relative);

    public static TheoryData<string, string?> Refused => new()
    {
        { "no header", null },
        { "another scheme", "Basic a3hfbGl2ZV86cGFzc3dvcmQ=" },
        { "an unknown key", "Bearer kx_live_ABCDEFGHIJKLMNOPQRSTUVWX" },
        { "a malformed key", "Bearer kx_live_short" },
        { "a key of another prefix", "Bearer zz_live_ABCDEFGHIJKLMNOPQRSTUVWX" },
        { "an empty token", "Bearer " },
    };

    [Theory]
    [MemberData(nameof(Refused))]
    public async Task Given_no_valid_key_When_v1_is_called_Then_401_invalid_api_key_with_WWW_Authenticate_Bearer(string because, string? authorization)
    {
        using var factory = new ApiFactory();
        using var client = Client(factory, key: null);
        if (authorization is not null)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authorization);
        }

        using var response = await client.GetAsync(Article, TestContext.Current.CancellationToken);

        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, because);
        Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).Scheme);
        Assert.Equal(("invalid_api_key", 401), await CodeAsync(response));
    }

    [Fact]
    public async Task Given_every_refusal_When_the_bodies_are_compared_Then_they_are_identical_but_for_the_trace()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        var revoked = await RevokedKeyAsync(factory);
        var bodies = new List<string>();
        foreach (var authorization in new[] { null, "Basic eDp5", "Bearer kx_live_ABCDEFGHIJKLMNOPQRSTUVWX", "Bearer kx_live_short", $"Bearer {revoked}" })
        {
            using var client = Client(factory, key: null);
            if (authorization is not null)
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authorization);
            }

            using var response = await client.GetAsync(Article, TestContext.Current.CancellationToken);
            var body = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>(TestContext.Current.CancellationToken);
            body!.Remove("trace_id");
            bodies.Add($"{(int)response.StatusCode} {JsonSerializer.Serialize(body.OrderBy(entry => entry.Key, StringComparer.Ordinal))}");
        }

        Assert.Single(bodies.Distinct(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Given_an_active_key_When_it_is_sent_as_a_Bearer_token_Then_the_request_is_authenticated()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        using var client = Client(factory, await KeyAsync(factory, "articles:read"));

        using var response = await client.GetAsync(Article, TestContext.Current.CancellationToken);

        Assert.Equal(("article_not_found", 404), await CodeAsync(response));
    }

    [Theory]
    [InlineData("Token")]
    [InlineData("Basic")]
    [InlineData("ApiKey")]
    public async Task Given_an_active_key_under_another_scheme_When_it_is_sent_Then_it_is_401(string scheme)
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        var key = await KeyAsync(factory, "articles:read");
        using var client = Client(factory, key: null);
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"{scheme} {key}");

        using var response = await client.GetAsync(Article, TestContext.Current.CancellationToken);

        Assert.Equal(("invalid_api_key", 401), await CodeAsync(response));
    }

    [Fact]
    public async Task Given_a_key_that_shares_a_real_key_prefix_When_it_is_sent_Then_it_is_401()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        var key = await KeyAsync(factory, "articles:read");
        var forged = key[..^1] + (key[^1] == 'A' ? 'B' : 'A');
        using var client = Client(factory, forged);

        using var response = await client.GetAsync(Article, TestContext.Current.CancellationToken);

        Assert.Equal(("invalid_api_key", 401), await CodeAsync(response));
    }

    [Theory]
    [InlineData("api_key")]
    [InlineData("access_token")]
    [InlineData("key")]
    public async Task Given_a_key_in_the_query_string_When_v1_is_called_Then_it_is_not_a_credential(string parameter)
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        var key = await KeyAsync(factory, "articles:read");
        using var client = Client(factory, key: null);

        using var response = await client.GetAsync(new Uri($"{Article}?{parameter}={key}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(("invalid_api_key", 401), await CodeAsync(response));
    }

    [Fact]
    public async Task Given_a_key_is_revoked_When_it_sends_its_next_request_Then_it_is_401()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        var key = await KeyAsync(factory, "articles:read");
        using var client = Client(factory, key);
        using (var before = await client.GetAsync(Article, TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.NotFound, before.StatusCode);
        }

        using var staff = await StaffClient.OpenAsync(factory);
        var listed = await staff.GetFromJsonAsync<JsonElement>(new Uri("/admin/v1/api-clients", UriKind.Relative), TestContext.Current.CancellationToken);
        var id = listed.GetProperty("items")[0].GetProperty("id").GetString();
        using (await staff.PostAsync(new Uri($"/admin/v1/api-clients/{id}/revoke", UriKind.Relative), null, TestContext.Current.CancellationToken))
        {
        }

        using var after = await client.GetAsync(Article, TestContext.Current.CancellationToken);

        Assert.Equal(("invalid_api_key", 401), await CodeAsync(after));
    }

    [Fact]
    public async Task Given_a_staff_session_and_no_key_When_v1_is_called_Then_it_is_401()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        using var staff = await StaffClient.OpenAsync(factory);

        using var response = await staff.GetAsync(Article, TestContext.Current.CancellationToken);

        Assert.Equal(("invalid_api_key", 401), await CodeAsync(response));
    }

    [Fact]
    public async Task Given_an_api_key_When_the_admin_api_is_called_Then_it_is_401()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        using var client = Client(factory, await KeyAsync(factory, "articles:read", "events:read", "sources:read"));

        using var response = await client.GetAsync(new Uri("/admin/v1/api-clients", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Given_a_key_without_articles_read_When_articles_are_read_Then_403_insufficient_scope_names_it()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        using var client = Client(factory, await KeyAsync(factory, "events:read", "sources:read"));

        using var response = await client.GetAsync(Article, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(("insufficient_scope", "articles:read"), (problem.GetProperty("code").GetString(), problem.GetProperty("required_scope").GetString()));
    }

    private static async Task<string> RevokedKeyAsync(ApiFactory factory)
    {
        var key = await KeyAsync(factory, "articles:read");
        using var staff = await StaffClient.OpenAsync(factory);
        var listed = await staff.GetFromJsonAsync<JsonElement>(new Uri("/admin/v1/api-clients", UriKind.Relative), TestContext.Current.CancellationToken);
        var id = listed.GetProperty("items")[0].GetProperty("id").GetString();
        using (await staff.PostAsync(new Uri($"/admin/v1/api-clients/{id}/revoke", UriKind.Relative), null, TestContext.Current.CancellationToken))
        {
        }

        return key;
    }

    private static async Task<(string?, int)> CodeAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return (problem.GetProperty("code").GetString(), problem.GetProperty("status").GetInt32());
    }
}
