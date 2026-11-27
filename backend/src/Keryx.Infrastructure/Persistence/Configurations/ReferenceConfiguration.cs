using Keryx.Domain.Reference;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Keryx.Infrastructure.Persistence.Configurations;

/// <summary>Regions and categories: each keyed by its slug, which never changes.</summary>
internal sealed class ReferenceConfiguration : IEntityTypeConfiguration<Region>, IEntityTypeConfiguration<Category>
{
    public const int SlugLength = 64;
    private const int NameLength = 120;

    public void Configure(EntityTypeBuilder<Region> builder)
    {
        builder.ToTable("Regions");
        builder.HasKey(region => region.Slug);
        builder.Property(region => region.Slug).HasMaxLength(SlugLength);
        builder.Property(region => region.Name).HasMaxLength(NameLength);
        builder.Property(region => region.TimeZone).HasMaxLength(SlugLength);
        builder.HasIndex(region => region.Rank);
    }

    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(category => category.Slug);
        builder.Property(category => category.Slug).HasMaxLength(SlugLength);
        builder.Property(category => category.Name).HasMaxLength(NameLength);
    }
}
