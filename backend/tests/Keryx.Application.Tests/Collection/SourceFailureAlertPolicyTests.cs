using Keryx.Domain.Alerts;
using Keryx.Domain.Collection;

namespace Keryx.Application.Tests.Collection;

// Traces to: L2-077 AC1, AC2, AC3
// One case per row: whether a source's issue raises its alert, with a threshold of 3.
public sealed class SourceFailureAlertPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 15, 0, 0, TimeSpan.Zero);

    public enum Standing
    {
        Open,
        Muted,
        Closed,
        Alerted,
    }

    [Theory]
    [InlineData("below the threshold", IssueKind.SourceNotFound, 2, Standing.Open, false)]
    [InlineData("at the threshold", IssueKind.SourceNotFound, 3, Standing.Open, true)]
    [InlineData("beyond it, never alerted (the threshold was lowered)", IssueKind.SourceTimeout, 5, Standing.Open, true)]
    [InlineData("beyond it, already alerted for this streak", IssueKind.SourceNotFound, 4, Standing.Alerted, false)]
    [InlineData("muted", IssueKind.SourceBlocked, 3, Standing.Muted, false)]
    [InlineData("closed: the source recovered", IssueKind.SourceInvalid, 3, Standing.Closed, false)]
    [InlineData("items held, a warning and not a failure", IssueKind.ItemsHeld, 3, Standing.Open, false)]
    public void ShouldAlert(string because, IssueKind kind, int streak, Standing state, bool alerts)
    {
        var issue = RunIssue.ForSource(RunWorker.Collection, IssueSeverity.Error, kind, 7, "detail", Now);
        for (var run = 1; run < streak; run++)
        {
            issue.Continue("detail");
        }

        if (state == Standing.Muted)
        {
            issue.Mute();
        }

        if (state == Standing.Closed)
        {
            issue.Close();
        }

        var record = state == Standing.Alerted ? AlertRecord.ForIssue(issue) : null;

        Assert.True(alerts == SourceFailureAlertPolicy.ShouldAlert(issue, 3, record), because);
    }
}
