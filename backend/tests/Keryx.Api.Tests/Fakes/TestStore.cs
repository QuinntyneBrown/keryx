using Keryx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Keryx.Api.Tests.Fakes;

/// <summary>
/// No real database: the tests' store is EF Core InMemory, one per fixture. Processes that name
/// the same store (a CLI run, then another, then a reader) see the same data.
/// </summary>
public static class TestStore
{
    /// <summary>A connection string that names the provider and reaches nothing.</summary>
    public const string UnusedConnection = "Host=unused.invalid;Database=unused";

    // One root for the test process, so a store's name alone decides what a context sees.
    private static readonly InMemoryDatabaseRoot Root = new();

    /// <summary>A store name no other fixture uses.</summary>
    public static string NewName() => $"keryx-tests-{Guid.NewGuid():N}";

    /// <summary>Replaces the PostgreSQL context with the InMemory store <paramref name="name"/>.</summary>
    public static IServiceCollection UseTestStore(this IServiceCollection services, string name)
    {
        // Both hold the provider: leaving either keeps Npgsql configured beside InMemory.
        services.RemoveAll<DbContextOptions<KeryxDbContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<KeryxDbContext>>();
        services.AddDbContext<KeryxDbContext>(options => options.UseInMemoryDatabase(name, Root));
        return services;
    }

    /// <summary>A context on the store <paramref name="name"/>, for a test to read what a process left.</summary>
    public static KeryxDbContext Open(string name) =>
        new(new DbContextOptionsBuilder<KeryxDbContext>().UseInMemoryDatabase(name, Root).Options);
}
