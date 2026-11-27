using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;

namespace Keryx.Api.Tests.Clients;

// Acceptance Test
// Traces to: L2-036 AC1 (revoked_at), AC2, AC3, AC4; L2-035 AC4
// Description: Staff revoke a key. Revocation is recorded once and never undone; a revoked client
// stays listed, and its product name is free for a new key.
public sealed class RevokeApiClientTests
{
    private static readonly Uri Clients = new("/admin/v1/api-clients", UriKind.Relative);

    [Fact]
    public async Task Given_an_active_client_When_it_is_revoked_Then_200_with_revoked_at_now_and_it_stays_listed()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);
        await CreateApiClientTests.CreateAsync(client, "Kingdom Strong website widget");
        var id = await OnlyIdAsync(client);
        factory.Time.Advance(TimeSpan.FromMinutes(5));
        var revokedAt = factory.Time.GetUtcNow();

        using var response = await RevokeAsync(client, id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(id, body.GetProperty("client").GetProperty("id").GetString());
        Assert.Equal(revokedAt, body.GetProperty("client").GetProperty("revoked_at").GetDateTimeOffset());
        var listed = await client.GetFromJsonAsync<JsonElement>(Clients, TestContext.Current.CancellationToken);
        var item = Assert.Single(listed.GetProperty("items").EnumerateArray());
        Assert.Equal(revokedAt, item.GetProperty("revoked_at").GetDateTimeOffset());
    }

    [Fact]
    public async Task Given_a_revoked_client_When_it_is_revoked_again_Then_200_and_revoked_at_is_unchanged()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);
        await CreateApiClientTests.CreateAsync(client, "Kingdom Strong website widget");
        var id = await OnlyIdAsync(client);
        var firstAt = factory.Time.GetUtcNow();
        using (await RevokeAsync(client, id))
        {
        }

        factory.Time.Advance(TimeSpan.FromMinutes(5));
        using var again = await RevokeAsync(client, id);

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        var body = await again.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(firstAt, body.GetProperty("client").GetProperty("revoked_at").GetDateTimeOffset());
    }

    [Theory]
    [InlineData("cli_doesnotexist0000000")]
    [InlineData("not-an-id")]
    public async Task Given_an_unknown_id_When_it_is_revoked_Then_404_client_not_found(string id)
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);

        using var response = await RevokeAsync(client, id);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("client_not_found", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Given_a_revoked_client_When_a_key_is_created_with_its_name_Then_it_is_created_and_both_are_listed()
    {
        using var factory = new ApiFactory();
        await StaffClient.SeedAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);
        await CreateApiClientTests.CreateAsync(client, "Kingdom Strong website widget");
        using (await RevokeAsync(client, await OnlyIdAsync(client)))
        {
        }

        factory.Time.Advance(TimeSpan.FromMinutes(1));
        await CreateApiClientTests.CreateAsync(client, "Kingdom Strong website widget");

        var listed = await client.GetFromJsonAsync<JsonElement>(Clients, TestContext.Current.CancellationToken);
        Assert.Equal([true, false], listed.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("revoked_at").ValueKind == JsonValueKind.Null));
    }

    private static Task<HttpResponseMessage> RevokeAsync(HttpClient client, string id) =>
        client.PostAsync(new Uri($"/admin/v1/api-clients/{id}/revoke", UriKind.Relative), null, TestContext.Current.CancellationToken);

    private static async Task<string> OnlyIdAsync(HttpClient client)
    {
        var listed = await client.GetFromJsonAsync<JsonElement>(Clients, TestContext.Current.CancellationToken);
        return listed.GetProperty("items")[0].GetProperty("id").GetString()!;
    }
}
