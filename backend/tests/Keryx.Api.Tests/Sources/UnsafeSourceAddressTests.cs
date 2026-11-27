using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Session;

namespace Keryx.Api.Tests.Sources;

// Acceptance Test
// Traces to: L2-024
// Description: A source whose host is not a public address is refused, and nothing is stored.
public sealed class UnsafeSourceAddressTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData("https://intranet.gracechurch.ca/feed")] // resolves to 10.0.0.5
    [InlineData("https://split.gracechurch.ca/feed")] // one public and one private address
    [InlineData("http://127.0.0.1/feed")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://[::ffff:10.0.0.1]/feed")]
    [InlineData("http://[::1]/feed")]
    public async Task Given_a_host_that_is_not_public_When_a_source_is_created_Then_it_is_400_unsafe_address(string address)
    {
        factory.Hosts.Answer("intranet.gracechurch.ca", "10.0.0.5");
        factory.Hosts.Answer("split.gracechurch.ca", "93.184.216.34", "192.168.1.20");
        await StaffClient.SeedAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);

        using var response = await client.PostAsJsonAsync(new Uri("/admin/v1/sources", UriKind.Relative), new
        {
            kind = "website",
            address,
            organization_name = "Grace Church",
            region = "toronto",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("unsafe_address", problem.GetProperty("code").GetString());
        Assert.Equal(["address"], problem.GetProperty("errors").EnumerateObject().Select(entry => entry.Name));
    }
}
