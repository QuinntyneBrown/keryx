using FluentValidation;
using Keryx.Api.Tests.Fakes;
using Keryx.Api.Tests.Probes;
using Keryx.Application.Sources;
using Keryx.Infrastructure.Persistence.Schema;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Keryx.Api.Tests;

/// <summary>
/// The API as it is deployed (environment Production), with no real database and no real
/// network: one EF Core InMemory store per fixture, the clock a test advances, and fakes behind
/// the contracts. A derived fixture overrides <see cref="ConfigureServices"/> to inject a fault.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    /// <summary>The fixture's store. A CLI run in the same test names it to seed what the API reads.</summary>
    public string Store { get; } = TestStore.NewName();

    /// <summary>The fixture's clock. It starts on a fixed instant, in UTC.</summary>
    public SignallingTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 27, 15, 0, 0, TimeSpan.Zero));

    /// <summary>Every line the API logs.</summary>
    public CapturedLogs Logs { get; } = new();

    /// <summary>The sources: every address answers what a test gave it, or not found.</summary>
    public FakeSourceFetcher Sources { get; } = new();

    /// <summary>DNS: every host resolves to a public address unless a test says otherwise.</summary>
    public FakeHostResolver Hosts { get; } = new();

    /// <summary>The migrations, which the API must never touch: a test asserts it was never called.</summary>
    public RecordingMigrator Migrator { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment(EnvironmentName);
        builder.UseSetting("Keryx:Oidc:Authority", "https://id.example.test");
        builder.UseSetting("Keryx:Oidc:ClientId", "keryx-test");
        builder.UseSetting("ConnectionStrings:Keryx", TestStore.UnusedConnection);
        builder.UseSetting("Keryx:Fetch:UserAgentContactUrl", "https://keryx.example/about-our-collector");
        // No OTLP endpoint, whatever the machine's environment says: a test reaches no network.
        builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", string.Empty);
        builder.ConfigureTestServices(services =>
        {
            services.Configure<Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectOptions>("OpenIdConnect", options =>
                options.Configuration = new Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration
                {
                    Issuer = "https://id.example.test",
                    AuthorizationEndpoint = "https://id.example.test/authorize",
                });
            services.UseTestStore(Store);
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
            services.AddSingleton<ILoggerProvider>(Logs);
            services.RemoveAll<ISourceFetcher>();
            services.AddSingleton<ISourceFetcher>(Sources);
            services.RemoveAll<IHostResolver>();
            services.AddSingleton<IHostResolver>(Hosts);
            services.RemoveAll<IDatabaseMigrator>();
            services.AddSingleton<IDatabaseMigrator>(Migrator);

            // The probe endpoints of this assembly: a request to validate and a handler that throws.
            var probes = typeof(ProbeController).Assembly;
            services.AddControllers().AddApplicationPart(probes);
            services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(probes));
            services.AddValidatorsFromAssembly(probes, includeInternalTypes: true);
            services.AddSingleton<ProbeCounter>();

            ConfigureServices(services);
        });
    }

    /// <summary>The host environment: Production, as deployed, unless a fixture proves a rule of another.</summary>
    protected virtual string EnvironmentName => "Production";

    /// <summary>Replaces services for one fixture: a fake behind a contract, or a fault.</summary>
    protected virtual void ConfigureServices(IServiceCollection services)
    {
    }
}

/// <summary>The API against a database in the state a test chooses.</summary>
public sealed class SchemaApiFactory(SchemaState state) : ApiFactory
{
    public FakeSchemaVersion Schema { get; } = new(state);

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.RemoveAll<ISchemaVersion>();
        services.AddSingleton<ISchemaVersion>(Schema);
    }
}
