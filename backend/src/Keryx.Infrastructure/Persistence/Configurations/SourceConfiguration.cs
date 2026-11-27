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
        builder.Property(source => source.Kind).HasConversion(kind => Word(kind), word => Kind(word)).HasMaxLength(16);
        builder.Property(source => source.Address).HasMaxLength(2048);
        builder.Property(source => source.OrganizationName).HasMaxLength(120);
        builder.Property(source => source.RegionSlug).HasMaxLength(ReferenceConfiguration.SlugLength);
        builder.Property(source => source.SeedKey).HasMaxLength(ReferenceConfiguration.SlugLength);
        builder.HasOne<Region>().WithMany().HasForeignKey(source => source.RegionSlug).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(source => source.SeedKey).IsUnique();
    }

    private static string Word(SourceKind kind) => kind switch
    {
        SourceKind.Rss => "rss",
        SourceKind.Website => "website",
        SourceKind.Instagram => "instagram",
        SourceKind.Eventbrite => "eventbrite",
        SourceKind.NewsQuery => "news_query",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown source kind."),
    };

    private static SourceKind Kind(string word) => word switch
    {
        "rss" => SourceKind.Rss,
        "website" => SourceKind.Website,
        "instagram" => SourceKind.Instagram,
        "eventbrite" => SourceKind.Eventbrite,
        "news_query" => SourceKind.NewsQuery,
        _ => throw new ArgumentOutOfRangeException(nameof(word), word, "Unknown source kind."),
    };
}
