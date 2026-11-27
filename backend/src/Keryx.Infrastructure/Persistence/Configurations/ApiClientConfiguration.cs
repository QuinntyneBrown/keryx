using System.Text.Json;
using Keryx.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Keryx.Infrastructure.Persistence.Configurations;

/// <summary>
/// API clients. The key hash is unique, so a key finds one client; a product name is unique among
/// active clients only, so a name frees up when its client is revoked.
/// </summary>
internal sealed class ApiClientConfiguration : IEntityTypeConfiguration<ApiClient>
{
    public void Configure(EntityTypeBuilder<ApiClient> builder)
    {
        builder.ToTable("ApiClients");
        builder.HasKey(client => client.Id);
        builder.Property(client => client.PublicId).HasMaxLength(SourceConfiguration.PublicIdLength);
        builder.HasIndex(client => client.PublicId).IsUnique();
        builder.Property(client => client.ProductName).HasMaxLength(ApiClient.MaxNameLength);
        builder.Property(client => client.NormalizedName).HasMaxLength(ApiClient.MaxNameLength);
        builder.HasIndex(client => client.NormalizedName).IsUnique().HasFilter("\"RevokedAt\" IS NULL");
        builder.Property(client => client.KeyPrefix).HasMaxLength(ApiClient.PrefixLength);
        builder.Property(client => client.KeyHash).HasMaxLength(64);
        builder.HasIndex(client => client.KeyHash).IsUnique();
        builder.Property(client => client.Scopes)
            .HasConversion(
                scopes => JsonSerializer.Serialize(scopes, JsonSerializerOptions.Default),
                json => JsonSerializer.Deserialize<List<string>>(json, JsonSerializerOptions.Default) ?? new List<string>(),
                new ValueComparer<IReadOnlyList<string>>(
                    (left, right) => left!.SequenceEqual(right!),
                    scopes => scopes.Aggregate(0, (hash, scope) => HashCode.Combine(hash, scope)),
                    scopes => scopes.ToList()))
            .HasMaxLength(128);
        builder.Ignore(client => client.IsActive);
    }
}
