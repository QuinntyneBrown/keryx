using System.Text.Json;
using Keryx.Domain.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Keryx.Infrastructure.Persistence.Configurations;

/// <summary>
/// The audit trail (L2-085): the action stored as its word (<c>article_published</c>), the changed
/// fields' names as a small JSON array, and an index on the time for retention.
/// </summary>
internal sealed class AuditConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.SubjectId).HasMaxLength(255);
        builder.Property(entry => entry.Action)
            .HasConversion(action => JsonNamingPolicy.SnakeCaseLower.ConvertName(action.ToString()), word => Parse(word))
            .HasMaxLength(32);
        builder.Property(entry => entry.TargetId).HasMaxLength(64);
        builder.Property(entry => entry.ChangedFields).HasConversion(Tags.Converter, Tags.Comparer).HasMaxLength(1024);
        builder.HasIndex(entry => entry.OccurredAt);
    }

    private static AuditAction Parse(string word) => Enum.Parse<AuditAction>(word.Replace("_", string.Empty, StringComparison.Ordinal), ignoreCase: true);
}
