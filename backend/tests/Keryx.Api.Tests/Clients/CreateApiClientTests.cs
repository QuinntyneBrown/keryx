using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Keryx.Api.Tests.Session;
using Keryx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Keryx.Api.Tests.Clients;

// Acceptance Test
// Traces to: L2-035 AC1-AC5; L2-083
// Description: Staff create an API key for a product with chosen scopes. The full key is in the
// create response once; Keryx keeps only its SHA-256 hash and its first 12 characters.
public sealed class CreateApiClientTests
{
    private static readonly Uri Clients = new("/admin/v1/api-clients", UriKind.Relative);
    private static readonly string[] ArticlesRead = ["articles:read"];
    private static readonly string[] SourcesRead = ["sources:read"];
    private static readonly string[] ArticlesAndEvents = ["articles:read", "events:read"];

    [Fact]
    public async Task Given_a_product_and_scopes_When_a_key_is_created_Then_201_returns_the_client_and_the_full_key_once()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);

        using var response = await client.PostAsJsonAsync(Clients, new
        {
            product_name = "Covenant Cycling site",
            scopes = ArticlesAndEvents,
            // Fields the endpoint does not define are ignored (L2-083).
            id = "cli_mine",
            revoked_at = "2026-01-01T00:00:00Z",
            key_hash = "00",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var key = body.GetProperty("key").GetString()!;
        Assert.Matches("^kx_live_[A-Za-z0-9]{24,}$", key);
        var created = body.GetProperty("client");
        Assert.StartsWith("cli_", created.GetProperty("id").GetString(), StringComparison.Ordinal);
        Assert.NotEqual("cli_mine", created.GetProperty("id").GetString());
        Assert.Equal("Covenant Cycling site", created.GetProperty("product_name").GetString());
        Assert.Equal(key[..12], created.GetProperty("key_prefix").GetString());
        Assert.Equal(["articles:read", "events:read"], created.GetProperty("scopes").EnumerateArray().Select(scope => scope.GetString()));
        Assert.Equal(factory.Time.GetUtcNow(), created.GetProperty("created_at").GetDateTimeOffset());
        Assert.Equal(JsonValueKind.Null, created.GetProperty("revoked_at").ValueKind);

        // Only the hash is kept, and no endpoint returns more than the prefix (L2-035 AC2).
        await using var scope = factory.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<KeryxDbContext>().ApiClients.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key))), stored.KeyHash);
        var listed = await client.GetFromJsonAsync<JsonElement>(Clients, TestContext.Current.CancellationToken);
        var item = Assert.Single(listed.GetProperty("items").EnumerateArray());
        Assert.DoesNotContain(key, listed.GetRawText(), StringComparison.Ordinal);
        Assert.Equal(
            ["created_at", "error_rate", "errors_24h", "id", "key_prefix", "last_call_at", "product_name", "requests_24h", "revoked_at", "scopes"],
            item.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Given_two_keys_When_they_are_created_Then_each_is_different()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);

        var first = await CreateAsync(client, "First product");
        var second = await CreateAsync(client, "Second product");

        Assert.NotEqual(first, second);
        Assert.NotEqual(first[..12], second[..12]);
    }

    // L2-035 AC3, and the frontend's apiKeyFormValidation uses this case table (product name, scopes, refused field).
    [Theory]
    [InlineData("", "articles:read", "product_name")]
    [InlineData("   ", "articles:read", "product_name")]
    [InlineData(null, "articles:read", "product_name")]
    [InlineData("81", "articles:read", "product_name")]
    [InlineData("Covenant Cycling site", "", "scopes")]
    [InlineData("Covenant Cycling site", "articles:write", "scopes")]
    [InlineData("Covenant Cycling site", "articles:read,articles:read", "scopes")]
    public async Task Given_an_invalid_name_or_scopes_When_a_key_is_created_Then_400_names_the_field_and_no_key_is_created(string? name, string scopes, string field)
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);

        using var response = await client.PostAsJsonAsync(Clients, new
        {
            product_name = name == "81" ? new string('a', 81) : name,
            scopes = scopes.Split(',', StringSplitOptions.RemoveEmptyEntries),
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
        Assert.Equal([field], problem.GetProperty("errors").EnumerateObject().Select(entry => entry.Name));
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<KeryxDbContext>().ApiClients.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_a_name_of_80_characters_with_spaces_around_it_When_a_key_is_created_Then_it_is_stored_trimmed()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);
        var name = new string('a', 80);

        using var response = await client.PostAsJsonAsync(Clients, new { product_name = $"  {name}  ", scopes = SourcesRead }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(name, body.GetProperty("client").GetProperty("product_name").GetString());
    }

    [Theory]
    [InlineData("Covenant Cycling site")]
    [InlineData("covenant cycling SITE")]
    public async Task Given_a_name_an_active_client_uses_When_a_key_is_created_Then_409_client_name_taken(string again)
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);
        await CreateAsync(client, "Covenant Cycling site");

        using var response = await client.PostAsJsonAsync(Clients, new { product_name = again, scopes = ArticlesRead }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("client_name_taken", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Given_three_clients_When_the_list_is_read_by_pages_Then_it_is_newest_first_with_numbers_for_paging()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);
        foreach (var name in new[] { "Oldest", "Middle", "Newest" })
        {
            await CreateAsync(client, name);
            factory.Time.Advance(TimeSpan.FromMinutes(1));
        }

        var first = await client.GetFromJsonAsync<JsonElement>(new Uri("/admin/v1/api-clients?page=1&page_size=2", UriKind.Relative), TestContext.Current.CancellationToken);
        var second = await client.GetFromJsonAsync<JsonElement>(new Uri("/admin/v1/api-clients?page=2&page_size=2", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(["Newest", "Middle"], first.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("product_name").GetString()));
        Assert.Equal(["Oldest"], second.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("product_name").GetString()));
        Assert.Equal((3, 1, 2), (first.GetProperty("total").GetInt32(), first.GetProperty("page").GetInt32(), first.GetProperty("page_size").GetInt32()));
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page_size=51")]
    [InlineData("page_size=0")]
    public async Task Given_a_page_out_of_range_When_the_list_is_read_Then_400_invalid_page(string query)
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);

        using var response = await client.GetAsync(new Uri($"/admin/v1/api-clients?{query}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("invalid_page", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Given_a_test_environment_When_a_key_is_created_Then_it_starts_with_kx_test()
    {
        using var factory = new TestEnvironmentFactory();
        await StaffClient.SeedAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);

        Assert.Matches("^kx_test_[A-Za-z0-9]{24,}$", await CreateAsync(client, "Staging site"));
    }

    internal static async Task<string> CreateAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync(Clients, new { product_name = name, scopes = ArticlesRead }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("key").GetString()!;
    }

    private sealed class TestEnvironmentFactory : ApiFactory
    {
        protected override string EnvironmentName => "Staging";
    }
}
