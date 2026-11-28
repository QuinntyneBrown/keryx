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

/// <summary>One lease per worker; its version is the concurrency token that lets one claim win.</summary>
internal sealed class RunLeaseConfiguration : IEntityTypeConfiguration<RunLease>
{
    public void Configure(EntityTypeBuilder<RunLease> builder)
    {
        builder.ToTable("RunLeases");
        builder.HasKey(lease => lease.Worker);
        builder.Property(lease => lease.Worker).HasConversion(worker => RunWords.Worker(worker), word => RunWords.WorkerOf(word)).HasMaxLength(16);
        builder.Property(lease => lease.ActiveRunId).HasMaxLength(SourceConfiguration.PublicIdLength);
        builder.Property(lease => lease.Version).IsConcurrencyToken();
    }
}

/// <summary>Issues and the runs they occurred in.</summary>
internal sealed class RunIssueConfiguration : IEntityTypeConfiguration<RunIssue>
{
    public void Configure(EntityTypeBuilder<RunIssue> builder)
    {
        builder.ToTable("RunIssues");
        builder.HasKey(issue => issue.Id);
        builder.Property(issue => issue.PublicId).HasMaxLength(SourceConfiguration.PublicIdLength);
        builder.HasIndex(issue => issue.PublicId).IsUnique();
        builder.Property(issue => issue.Worker).HasConversion(worker => RunWords.Worker(worker), word => RunWords.WorkerOf(word)).HasMaxLength(16);
        builder.Property(issue => issue.Severity).HasConversion(value => RunWords.Severity(value), word => RunWords.SeverityOf(word)).HasMaxLength(8);
        builder.Property(issue => issue.Kind).HasConversion(value => RunWords.Kind(value), word => RunWords.KindOf(word)).HasMaxLength(24);
        builder.Property(issue => issue.State).HasConversion(value => RunWords.State(value), word => RunWords.StateOf(word)).HasMaxLength(8);
        builder.Property(issue => issue.Stage).HasConversion(
            value => value == null ? null : RunWords.Stage(value.Value),
            word => word == null ? null : RunWords.StageOf(word)).HasMaxLength(16);
        builder.Property(issue => issue.Detail).HasMaxLength(300);
        builder.HasOne<Keryx.Domain.Sources.Source>().WithMany().HasForeignKey(issue => issue.SourceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(issue => new { issue.State, issue.Severity, issue.Since });
    }
}

internal sealed class IssueOccurrenceConfiguration : IEntityTypeConfiguration<IssueOccurrence>
{
    public void Configure(EntityTypeBuilder<IssueOccurrence> builder)
    {
        builder.ToTable("IssueOccurrences");
        builder.HasKey(occurrence => occurrence.Id);
        builder.Property(occurrence => occurrence.Detail).HasMaxLength(300);
        builder.HasOne(occurrence => occurrence.Issue).WithMany().HasForeignKey(occurrence => occurrence.IssueId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(occurrence => occurrence.Run).WithMany().HasForeignKey(occurrence => occurrence.RunId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(occurrence => new { occurrence.RunId, occurrence.IssueId }).IsUnique();
    }
}

internal sealed class AlertRecordConfiguration : IEntityTypeConfiguration<Keryx.Domain.Alerts.AlertRecord>
{
    public void Configure(EntityTypeBuilder<Keryx.Domain.Alerts.AlertRecord> builder)
    {
        builder.ToTable("AlertRecords");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Kind).HasConversion(
            kind => kind == Keryx.Domain.Alerts.AlertKind.SourceFailing ? "source_failing" : "run_failed",
            word => word == "source_failing" ? Keryx.Domain.Alerts.AlertKind.SourceFailing : Keryx.Domain.Alerts.AlertKind.RunFailed).HasMaxLength(16);
        // One alert per issue (once per streak) and one per run.
        builder.HasOne<RunIssue>().WithMany().HasForeignKey(record => record.IssueId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(record => record.IssueId).IsUnique();
        builder.HasOne<Run>().WithMany().HasForeignKey(record => record.RunId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(record => record.RunId).IsUnique();
    }
}
