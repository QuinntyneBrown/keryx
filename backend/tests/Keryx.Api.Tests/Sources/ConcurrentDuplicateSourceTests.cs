using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Keryx.Api.Tests.Fakes;
using Keryx.Api.Tests.Session;
using Keryx.Application.Abstractions;
using Keryx.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Keryx.Api.Tests.Sources;

// Acceptance Test
// Traces to: L2-001
// Description: Two requests that add the same source at once leave one source and one 409.
public sealed class ConcurrentDuplicateSourceTests(ConcurrentDuplicateSourceTests.Factory factory)
    : IClassFixture<ConcurrentDuplicateSourceTests.Factory>
{
    // L2-001 AC3: the request that loses the race to the unique index is source_exists.
    [Fact]
    public async Task Given_another_request_saved_the_same_source_first_When_this_one_saves_Then_it_is_409_source_exists()
    {
        await StaffClient.SeedAsync(factory);
        using var client = await StaffClient.OpenAsync(factory);
        factory.Fault.Armed = true;

        using var response = await client.PostAsJsonAsync(new Uri("/admin/v1/sources", UriKind.Relative), new
        {
            kind = "rss",
            address = "https://gracechurch.ca/race",
            organization_name = "Grace Church",
            region = "toronto",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("source_exists", problem.GetProperty("code").GetString());
    }

    public sealed class Factory : ApiFactory
    {
        public SaveFault.Switch Fault { get; } = new();

        protected override void ConfigureServices(IServiceCollection services)
        {
            services.RemoveAll<IAppDbContext>();
            services.AddScoped<IAppDbContext>(provider => new SaveFault(provider.GetRequiredService<KeryxDbContext>(), Fault));
        }
    }
}
