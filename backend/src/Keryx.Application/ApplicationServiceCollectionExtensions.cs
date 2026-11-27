using FluentValidation;
using Keryx.Application.Behaviors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Keryx.Application;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// The MediatR handlers and FluentValidation validators of this assembly. Every request is
    /// validated before its handler runs (<see cref="ValidationBehavior{TRequest, TResponse}"/>).
    /// Time is read from <see cref="TimeProvider"/>, never from the system clock directly, so a
    /// test advances it.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(ApplicationServiceCollectionExtensions).Assembly;
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(assembly);
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<Sources.SafeAddressCheck>();
        services.AddScoped<Sources.SourceTester>();
        services.AddSingleton<Scoring.IRelevanceScorer, Scoring.RelevanceScorer>();
        return services;
    }
}
