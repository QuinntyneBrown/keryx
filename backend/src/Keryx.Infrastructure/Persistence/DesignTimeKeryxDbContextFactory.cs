using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Keryx.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> build the model without a running app. Adding a migration
/// connects to nothing: the connection string only names the provider.
/// </summary>
internal sealed class DesignTimeKeryxDbContextFactory : IDesignTimeDbContextFactory<KeryxDbContext>
{
    public KeryxDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<KeryxDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__Keryx") ?? "Host=localhost;Database=keryx")
            .Options);
}
