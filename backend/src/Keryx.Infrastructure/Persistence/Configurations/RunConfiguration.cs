using System.Text.Json;
using Keryx.Application.Runs;
using Keryx.Domain.Collection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Keryx.Infrastructure.Persistence.Configurations;

/// <summary>
/// Runs, their counts and their six stage results. Words are stored as the API writes them, and a
/// stage's named counts as a small JSON object.
/// </summary>
internal sealed class RunConfiguration : IEntityTypeConfiguration<Run>
{
    public void Configure(EntityTypeBuilder<Run> builder)
    {
        builder.ToTable("Runs");
        builder.HasKey(run => run.Id);
        builder.Property(run => run.PublicId).HasMaxLength(SourceConfiguration.PublicIdLength);
        builder.HasIndex(run => run.PublicId).IsUnique();
        builder.Property(run => run.Worker).HasConversion(worker => RunWords.Worker(worker), word => RunWords.WorkerOf(word)).HasMaxLength(16);
        builder.Property(run => run.Trigger).HasConversion(trigger => RunWords.Trigger(trigger), word => RunWords.TriggerOf(word)).HasMaxLength(16);
        builder.Property(run => run.Outcome).HasConversion(outcome => RunWords.Outcome(outcome), word => RunWords.OutcomeOf(word)).HasMaxLength(24);
        builder.Property(run => run.FailureReason).HasMaxLength(300);
        builder.Property(run => run.CorrelationId).HasMaxLength(64);
        builder.HasIndex(run => new { run.Worker, run.StartedAt });
        builder.OwnsOne(run => run.Counts);
        builder.OwnsMany(run => run.Stages, stage =>
        {
            stage.ToTable("RunStages");
            stage.WithOwner().HasForeignKey("RunId");
            stage.HasKey("RunId", nameof(StageResult.Stage));
            stage.Property(result => result.Stage).HasConversion(value => RunWords.Stage(value), word => RunWords.StageOf(word)).HasMaxLength(16);
            stage.Property(result => result.Status).HasConversion(value => RunWords.Status(value), word => RunWords.StatusOf(word)).HasMaxLength(24);
            stage.Property(result => result.Reason).HasMaxLength(300);
            stage.Property(result => result.Counts)
                .HasConversion(
                    counts => JsonSerializer.Serialize(counts, JsonSerializerOptions.Default),
                    json => JsonSerializer.Deserialize<Dictionary<string, int>>(json, JsonSerializerOptions.Default) ?? new Dictionary<string, int>(),
                    new ValueComparer<IDictionary<string, int>>(
                        (left, right) => left!.Count == right!.Count && !left.Except(right).Any(),
                        counts => counts.Aggregate(0, (hash, pair) => HashCode.Combine(hash, pair.Key, pair.Value)),
                        counts => new Dictionary<string, int>(counts)))
                .HasMaxLength(1024);
        });
        builder.Navigation(run => run.Stages).HasField("_stages");
    }
}
