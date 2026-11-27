using MediatR;

namespace Keryx.Application.Seeding;

/// <summary>
/// Adds the seed data that is missing (L2-095). A row that already exists is left as it is, so a
/// second run changes nothing.
/// </summary>
public sealed record SeedDataCommand : IRequest<SeedDataResult>;

/// <summary>How many rows of each kind this run added. A run that changed nothing adds none.</summary>
public sealed record SeedDataResult(int Regions, int Categories, int Settings, int Sources)
{
    public int Total => Regions + Categories + Settings + Sources;
}
