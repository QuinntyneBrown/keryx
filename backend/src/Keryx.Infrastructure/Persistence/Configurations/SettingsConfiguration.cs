using Keryx.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Keryx.Infrastructure.Persistence.Configurations;

/// <summary>The one settings row. Its lists are PostgreSQL arrays of text (ADR 0001).</summary>
internal sealed class SettingsConfiguration : IEntityTypeConfiguration<Settings>
{
    public void Configure(EntityTypeBuilder<Settings> builder)
    {
        // There is one row and its id is fixed: the database never issues one and refuses a second.
        builder.ToTable("Settings", table => table.HasCheckConstraint("CK_Settings_Singleton", "\"Id\" = 1"));
        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.Id).ValueGeneratedNever();
        builder.Property(settings => settings.PublishThreshold).HasPrecision(3, 2);
        builder.Property(settings => settings.AlertEmail).HasMaxLength(254);
    }
}
