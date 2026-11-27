using Keryx.Domain.Collection;

namespace Keryx.Application.Tests.Collection;

/// <summary>How a run's outcome follows from its stages and sources (L2-007).</summary>
public sealed class RunOutcomeTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 17, 0, 0, TimeSpan.Zero);

    [Theory]
    // AC1: every stage done.
    [InlineData("done", "done", 0, RunOutcome.Succeeded)]
    // Held items alone are the classify stage's warning, not the run's: the run succeeded.
    [InlineData("done", "done_with_warnings", 0, RunOutcome.Succeeded)]
    // AC2: a source failed.
    [InlineData("done_with_warnings", "done", 2, RunOutcome.CompletedWithIssues)]
    [InlineData("done_with_warnings", "done_with_warnings", 1, RunOutcome.CompletedWithIssues)]
    public void Given_finished_stages_When_the_run_finishes_Then_its_outcome_follows(string fetch, string classify, int failed, RunOutcome outcome)
    {
        var run = Run.Start(RunWorker.Collection, RunTrigger.Schedule, Now);
        run.RecordStage(PipelineStage.Fetch, Status(fetch), 1, new Dictionary<string, int>());
        run.RecordStage(PipelineStage.Classify, Status(classify), 1, new Dictionary<string, int>());

        run.Finish(new RunCounts { SourcesFailed = failed }, Now.AddMinutes(1));

        Assert.Equal(outcome, run.Outcome);
    }

    [Fact]
    public void Given_a_stage_fails_When_the_run_fails_there_Then_later_stages_are_not_run_and_the_outcome_is_failed()
    {
        var run = Run.Start(RunWorker.Collection, RunTrigger.Schedule, Now);
        run.RecordStage(PipelineStage.Fetch, StageStatus.Done, 10, new Dictionary<string, int>());
        // Items stream from stage to stage, so a later stage may already be running.
        run.RecordStage(PipelineStage.Publish, StageStatus.Running, 0, new Dictionary<string, int> { ["published"] = 3 });

        run.FailStage(PipelineStage.Store, "The store stage could not save the run's records.", Now.AddMinutes(2));

        Assert.Equal(RunOutcome.Failed, run.Outcome);
        Assert.Equal(Now.AddMinutes(2), run.FinishedAt);
        Assert.Equal(
            [StageStatus.Done, StageStatus.NotRun, StageStatus.NotRun, StageStatus.NotRun, StageStatus.Failed, StageStatus.NotRun],
            run.Stages.OrderBy(stage => stage.Stage).Select(stage => stage.Status));
        Assert.Equal("The store stage could not save the run's records.", run.Stages.Single(stage => stage.Stage == PipelineStage.Store).Reason);
    }

    [Fact]
    public void Given_a_run_When_it_fails_outright_Then_it_keeps_a_generic_reason_and_the_correlation_id()
    {
        var run = Run.Start(RunWorker.Retention, RunTrigger.Schedule, Now);

        run.Fail("The run stopped unexpectedly.", "4bf92f3577b34da6a3ce929d0e0e4736", Now.AddSeconds(3));

        Assert.Equal(RunOutcome.Failed, run.Outcome);
        Assert.Equal("The run stopped unexpectedly.", run.FailureReason);
        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", run.CorrelationId);
        Assert.Equal(3_000, run.DurationMs);
    }

    private static StageStatus Status(string word) => word switch
    {
        "done" => StageStatus.Done,
        "done_with_warnings" => StageStatus.DoneWithWarnings,
        _ => throw new ArgumentOutOfRangeException(nameof(word)),
    };
}
