using Keryx.Application.Sources;
using Keryx.Domain.Reference;
using Keryx.Domain.Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Keryx.Infrastructure.Persistence.Configurations;

/// <summary>
/// Sources. The kind is stored as the word the API uses (<c>news_query</c>), and a seeded
/// source's key is unique, so seeding the same source twice cannot succeed.
/// </summary>
internal sealed class SourceConfiguration : IEntityTypeConfiguration<Source>
{
    public void Configure(EntityTypeBuilder<Source> builder)
    {
        builder.ToTable("Sources");
        builder.HasKey(source => source.Id);
        builder.Property(source => source.Kind).HasConversion(kind => SourceWords.Kind(kind), word => SourceWords.KindOf(word)!.Value).HasMaxLength(16);
        builder.Property(source => source.Address).HasMaxLength(2048);
        builder.Property(source => source.OrganizationName).HasMaxLength(120);
        builder.Property(source => source.RegionSlug).HasMaxLength(ReferenceConfiguration.SlugLength);
        builder.Property(source => source.SeedKey).HasMaxLength(ReferenceConfiguration.SlugLength);
        builder.HasOne<Region>().WithMany().HasForeignKey(source => source.RegionSlug).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(source => source.SeedKey).IsUnique();
        builder.Property(source => source.NormalizedAddress).HasMaxLength(2048);
        // One source per kind and normalized address; a concurrent duplicate fails here (L2-001 AC3).
        builder.HasIndex(source => new { source.Kind, source.NormalizedAddress }).IsUnique();
        builder.Property(source => source.PublicId).HasMaxLength(PublicIdLength);
        builder.HasIndex(source => source.PublicId).IsUnique();
        builder.Property(source => source.Health).HasConversion(health => SourceWords.Health(health), word => SourceWords.HealthOf(word)!.Value).HasMaxLength(16);
    }

    /// <summary>A prefix of up to 3 letters, the underscore and 12 characters.</summary>
    internal const int PublicIdLength = 16;
}
