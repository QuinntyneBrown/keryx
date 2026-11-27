using System.Text.Json;
using Keryx.Domain.Catalogue;
using Keryx.Domain.Collection;
using Keryx.Domain.Reference;
using Keryx.Domain.Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Keryx.Infrastructure.Persistence.Configurations;

/// <summary>
/// Articles and their provenance. The canonical link is unique, so one story is one row; a
/// provenance entry keeps its source's name and URL when the source is removed.
/// </summary>
internal sealed class ArticleConfiguration : IEntityTypeConfiguration<Article>
{
    public const int UrlLength = 2048;

    public void Configure(EntityTypeBuilder<Article> builder)
    {
        builder.ToTable("Articles");
        builder.HasKey(article => article.Id);
        builder.Property(article => article.PublicId).HasMaxLength(SourceConfiguration.PublicIdLength);
        builder.HasIndex(article => article.PublicId).IsUnique();
        builder.Property(article => article.Url).HasMaxLength(UrlLength);
        builder.HasIndex(article => article.Url).IsUnique();
        builder.Property(article => article.Title).HasMaxLength(ArticleTitle.MaxLength);
        builder.Property(article => article.Summary).HasMaxLength(ArticleSummary.MaxLength);
        builder.Property(article => article.Category).HasMaxLength(ReferenceConfiguration.SlugLength);
        builder.Property(article => article.RegionSlug).HasMaxLength(ReferenceConfiguration.SlugLength);
        builder.HasOne<Region>().WithMany().HasForeignKey(article => article.RegionSlug).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Source>().WithMany().HasForeignKey(article => article.SourceId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(article => article.Organization).WithMany().HasForeignKey(article => article.OrganizationId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(article => article.Tags).HasConversion(Tags.Converter, Tags.Comparer).HasMaxLength(1300);
        builder.Property(article => article.Score).HasPrecision(3, 2);
        builder.Property(article => article.Status).HasConversion(Words.Status, Words.StatusOf).HasMaxLength(16);
        builder.Property(article => article.Fingerprint).HasMaxLength(64);
        builder.Property(article => article.StatusReason).HasMaxLength(32);
        builder.Property(article => article.Threshold).HasPrecision(3, 2);
        builder.Property(article => article.DecidedBy).HasConversion(by => by == DecisionMaker.Staff ? "staff" : "system", word => word == "staff" ? DecisionMaker.Staff : DecisionMaker.System).HasMaxLength(8);
        builder.Property(article => article.PreviousStatus).HasConversion(
            status => status == null ? null
                : status == PublicationStatus.Published ? "published" : status == PublicationStatus.NeedsReview ? "needs_review" : "hidden",
            word => word == null ? null
                : word == "published" ? PublicationStatus.Published : word == "needs_review" ? PublicationStatus.NeedsReview : PublicationStatus.Hidden)
            .HasMaxLength(16);
        builder.Property(article => article.PreviousDecidedBy).HasConversion(
            by => by == null ? null : by == DecisionMaker.Staff ? "staff" : "system",
            word => word == null ? null : word == "staff" ? DecisionMaker.Staff : DecisionMaker.System).HasMaxLength(8);
        builder.HasIndex(article => new { article.Status, article.CollectedAt });
        // A product's list: served articles, newest first, sought by (ListedAt, Id) (L2-026, L2-086).
        builder.HasIndex(article => new { article.Status, article.ListedAt, article.Id });
        builder.OwnsMany(article => article.Provenance, entry =>
        {
            entry.ToTable("ArticleProvenance");
            entry.WithOwner().HasForeignKey("ArticleId");
            entry.Property<long>("Id");
            entry.HasKey("Id");
            entry.Property(provenance => provenance.SourceName).HasMaxLength(120);
            entry.Property(provenance => provenance.Url).HasMaxLength(UrlLength);
            entry.HasOne<Source>().WithMany().HasForeignKey(provenance => provenance.SourceId).OnDelete(DeleteBehavior.SetNull);
        });
        builder.Navigation(article => article.Provenance).HasField("_provenance");
    }
}

/// <summary>Organizations, one per normalized name, with where they were seen.</summary>
internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("Organizations");
        builder.HasKey(organization => organization.Id);
        builder.Property(organization => organization.PublicId).HasMaxLength(SourceConfiguration.PublicIdLength);
        builder.HasIndex(organization => organization.PublicId).IsUnique();
        builder.Property(organization => organization.Name).HasMaxLength(120);
        builder.Property(organization => organization.NormalizedName).HasMaxLength(120);
        builder.HasIndex(organization => organization.NormalizedName).IsUnique();
        builder.Property(organization => organization.Kind).HasConversion(kind => OrganizationKinds.Word(kind), word => OrganizationKinds.Of(word)).HasMaxLength(16);
        builder.Property(organization => organization.RegionSlug).HasMaxLength(ReferenceConfiguration.SlugLength);
        builder.HasOne<Region>().WithMany().HasForeignKey(organization => organization.RegionSlug).OnDelete(DeleteBehavior.Restrict);
        builder.Property(organization => organization.WebsiteUrl).HasMaxLength(ArticleConfiguration.UrlLength);
        builder.OwnsMany(organization => organization.Provenance, entry =>
        {
            entry.ToTable("OrganizationProvenance");
            entry.WithOwner().HasForeignKey("OrganizationId");
            entry.Property<long>("Id");
            entry.HasKey("Id");
            entry.Property(provenance => provenance.SourceName).HasMaxLength(120);
            entry.Property(provenance => provenance.Url).HasMaxLength(ArticleConfiguration.UrlLength);
            entry.HasOne<Source>().WithMany().HasForeignKey(provenance => provenance.SourceId).OnDelete(DeleteBehavior.SetNull);
        });
        builder.Navigation(organization => organization.Provenance).HasField("_provenance");
    }
}

/// <summary>The changes a run has staged and not yet published.</summary>
internal sealed class StagedArticleConfiguration : IEntityTypeConfiguration<StagedArticle>
{
    public void Configure(EntityTypeBuilder<StagedArticle> builder)
    {
        builder.ToTable("StagedArticles");
        builder.HasKey(staged => staged.Id);
        builder.Ignore(staged => staged.Facts);
        builder.HasOne<Run>().WithMany().HasForeignKey(staged => staged.RunId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(staged => staged.Action).HasConversion<string>().HasMaxLength(8);
        builder.Property(staged => staged.SourceName).HasMaxLength(120);
        builder.Property(staged => staged.Url).HasMaxLength(ArticleConfiguration.UrlLength);
        builder.HasIndex(staged => new { staged.RunId, staged.Url });
        builder.Property(staged => staged.TargetUrl).HasMaxLength(ArticleConfiguration.UrlLength);
        builder.Property(staged => staged.OrganizationKey).HasMaxLength(120);
        builder.HasIndex(staged => new { staged.RunId, staged.OrganizationKey });
        builder.Property(staged => staged.Title).HasMaxLength(ArticleTitle.MaxLength);
        builder.Property(staged => staged.Summary).HasMaxLength(ArticleSummary.MaxLength);
        builder.Property(staged => staged.Category).HasMaxLength(ReferenceConfiguration.SlugLength);
        builder.Property(staged => staged.RegionSlug).HasMaxLength(ReferenceConfiguration.SlugLength);
        builder.Property(staged => staged.Tags).HasConversion(Tags.Converter, Tags.Comparer).HasMaxLength(1300);
        builder.Property(staged => staged.Score).HasPrecision(3, 2);
        builder.Property(staged => staged.Threshold).HasPrecision(3, 2);
        builder.Property(staged => staged.Status).HasConversion(Words.Status, Words.StatusOf).HasMaxLength(16);
        builder.Property(staged => staged.EventTimeZone).HasMaxLength(64);
        builder.Property(staged => staged.EventPlace).HasMaxLength(200);
        builder.Property(staged => staged.EventSignature).HasMaxLength(300);
        builder.Ignore(staged => staged.EventFacts);
    }
}

/// <summary>Events, each in UTC with its zone, with where they were seen.</summary>
internal sealed class EventConfiguration : IEntityTypeConfiguration<EventRecord>
{
    public void Configure(EntityTypeBuilder<EventRecord> builder)
    {
        builder.ToTable("Events");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.PublicId).HasMaxLength(SourceConfiguration.PublicIdLength);
        builder.HasIndex(item => item.PublicId).IsUnique();
        builder.Property(item => item.Title).HasMaxLength(ArticleTitle.MaxLength);
        builder.Property(item => item.TimeZone).HasMaxLength(64);
        builder.Property(item => item.PlaceName).HasMaxLength(200);
        builder.Property(item => item.PlaceKey).HasMaxLength(200);
        builder.Property(item => item.RegionSlug).HasMaxLength(ReferenceConfiguration.SlugLength);
        builder.HasOne<Region>().WithMany().HasForeignKey(item => item.RegionSlug).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Organization).WithMany().HasForeignKey(item => item.OrganizationId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(item => item.LinkedArticle).WithMany().HasForeignKey(item => item.LinkedArticleId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(item => new { item.OrganizationId, item.StartsAtUtc });
        // A product's list: soonest first, sought by (StartsAtUtc, Id) (L2-028, L2-086).
        builder.HasIndex(item => new { item.StartsAtUtc, item.Id });
        builder.Property(item => item.Score).HasPrecision(3, 2);
        builder.Property(item => item.Status).HasConversion(Words.Status, Words.StatusOf).HasMaxLength(16);
        builder.OwnsMany(item => item.Provenance, entry =>
        {
            entry.ToTable("EventProvenance");
            entry.WithOwner().HasForeignKey("EventId");
            entry.Property<long>("Id");
            entry.HasKey("Id");
            entry.Property(provenance => provenance.SourceName).HasMaxLength(120);
            entry.Property(provenance => provenance.Url).HasMaxLength(ArticleConfiguration.UrlLength);
            entry.HasOne<Source>().WithMany().HasForeignKey(provenance => provenance.SourceId).OnDelete(DeleteBehavior.SetNull);
        });
        builder.Navigation(item => item.Provenance).HasField("_provenance");
    }
}

internal static class Tags
{
    public static readonly Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<IReadOnlyList<string>, string> Converter = new(
        tags => JsonSerializer.Serialize(tags, JsonSerializerOptions.Default),
        json => JsonSerializer.Deserialize<List<string>>(json, JsonSerializerOptions.Default) ?? new List<string>());

    public static readonly ValueComparer<IReadOnlyList<string>> Comparer = new(
        (left, right) => left!.SequenceEqual(right!),
        tags => tags.Aggregate(0, (hash, tag) => HashCode.Combine(hash, tag)),
        tags => tags.ToList());
}

internal static class Words
{
    public static readonly System.Linq.Expressions.Expression<Func<PublicationStatus, string>> Status = status =>
        status == PublicationStatus.Published ? "published" : status == PublicationStatus.NeedsReview ? "needs_review" : "hidden";

    public static readonly System.Linq.Expressions.Expression<Func<string, PublicationStatus>> StatusOf = word =>
        word == "published" ? PublicationStatus.Published : word == "needs_review" ? PublicationStatus.NeedsReview : PublicationStatus.Hidden;
}
