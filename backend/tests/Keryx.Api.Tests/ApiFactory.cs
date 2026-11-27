using FluentValidation;
using Keryx.Api.Tests.Probes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Keryx.Api.Tests;

/// <summary>
/// The API as it is deployed (environment Production), with no real database and no real
/// network: one EF Core InMemory store per fixture, the clock a test advances, and fakes behind
/// the contracts. A derived fixture overrides <see cref="ConfigureServices"/> to inject a fault.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    /// <summary>The fixture's clock. It starts on a fixed instant, in UTC.</summary>
    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 27, 15, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment("Production");
        builder.UseSetting("ConnectionStrings:Keryx", "Host=unused.invalid;Database=unused");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);

            // The probe endpoints of this assembly: a request to validate and a handler that throws.
            var probes = typeof(ProbeController).Assembly;
            services.AddControllers().AddApplicationPart(probes);
            services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(probes));
            services.AddValidatorsFromAssembly(probes, includeInternalTypes: true);
            services.AddSingleton<ProbeCounter>();

            ConfigureServices(services);
        });
    }

    /// <summary>Replaces services for one fixture: a fake behind a contract, or a fault.</summary>
    protected virtual void ConfigureServices(IServiceCollection services)
    {
    }
}
