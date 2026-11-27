using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Fakes;
using Keryx.Api.Tests.Session;
using Keryx.Application.Abstractions;
using Keryx.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Keryx.Api.Tests.Clients;

// Acceptance Test
// Traces to: L2-035 AC4
// Description: Two creates with the same product name at once leave one client and one 409: the
// filtered unique index refuses the save that loses the race.
public sealed class ConcurrentClientNameTests
{
    private static readonly string[] ArticlesRead = ["articles:read"];

    [Fact]
    public async Task Given_another_request_saved_the_same_name_first_When_this_one_saves_Then_it_is_409_client_name_taken()
    {
        using var factory = new Factory();
        await StaffClient.SeedAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);
        factory.Fault.Armed = true;

        using var response = await client.PostAsJsonAsync(new Uri("/admin/v1/api-clients", UriKind.Relative),
            new { product_name = "Covenant Cycling site", scopes = ArticlesRead }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("client_name_taken", problem.GetProperty("code").GetString());
    }

    private sealed class Factory : ApiFactory
    {
        public SaveFault.Switch Fault { get; } = new();

        protected override void ConfigureServices(IServiceCollection services) =>
            services.AddScoped<IAppDbContext>(provider => new SaveFault(provider.GetRequiredService<KeryxDbContext>(), Fault));
    }
}
