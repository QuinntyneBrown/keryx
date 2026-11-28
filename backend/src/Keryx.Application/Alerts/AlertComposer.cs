using System.Globalization;
using System.Text.RegularExpressions;
using Keryx.Domain;
using Keryx.Domain.Collection;
using Keryx.Domain.Sources;
using Microsoft.Extensions.Options;

namespace Keryx.Application.Alerts;

/// <summary>
/// Writes each alert as plain text (L2-077 AC6), in the words of the alerts design. Its links lead
/// only to the admin app's origin; its reason is an issue's kind and bounded detail, never an
/// exception, a key or a connection string. Text from a source has its line breaks collapsed, so
/// it cannot forge a line.
/// </summary>
public sealed partial class AlertComposer(IOptions<AlertOptions> options)
{
    public MailMessage SourceFailing(string to, Source source, RunIssue issue, Run run)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(issue);
        ArgumentNullException.ThrowIfNull(run);
        var name = OneLine(source.OrganizationName);
        var streak = issue.ConsecutiveRuns.ToString(CultureInfo.InvariantCulture);
        return new MailMessage(
            to,
            $"Keryx: {name} has failed {streak} runs in a row",
            $"{name} has failed {streak} collection runs in a row, since {When(issue.Since)}.\n" +
            "\n" +
            $"Reason: {KindTitle(issue.Kind)}. {OneLine(issue.Detail)}\n" +
            "\n" +
            $"See the latest run: {Link($"/runs?run={run.PublicId}")}\n" +
            "\n" +
            "You won't get another e-mail about this until the source recovers and fails again. " +
            "To stop these e-mails for this problem, mute the issue on the run.\n" +
            Footer());
    }

    public MailMessage RunFailed(string to, Run run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var stage = run.Stages.FirstOrDefault(result => result.Status == StageStatus.Failed) is { } failed
            ? $"at the {Runs.RunWords.Stage(failed.Stage)} stage"
            : "before it finished";
        var reference = run.CorrelationId is { } correlation ? $"Reference: {correlation}\n" : string.Empty;
        return new MailMessage(
            to,
            "Keryx: a collection run failed",
            $"The collection run that started {When(run.StartedAt)} failed {stage}.\n" +
            "\n" +
            $"Reason: {OneLine(run.FailureReason ?? string.Empty)}\n" +
            reference +
            "\n" +
            $"See the run: {Link($"/runs?run={run.PublicId}")}\n" +
            Footer());
    }

    /// <summary>How many titles the digest lists (L2-078 AC1).</summary>
    public const int DigestTitles = 20;

    public MailMessage Digest(string to, int count, IReadOnlyList<string?> newest)
    {
        ArgumentNullException.ThrowIfNull(newest);
        var n = count.ToString(CultureInfo.InvariantCulture);
        var titles = string.Concat(newest.Select(title => $"- {OneLine(title ?? "Untitled post")}\n"));
        var more = count > newest.Count
            ? string.Create(CultureInfo.InvariantCulture, $"…and {count - newest.Count} more.\n")
            : string.Empty;
        return new MailMessage(
            to,
            count == 1 ? "Keryx: 1 article needs review" : $"Keryx: {n} articles need review",
            (count == 1 ? "1 article is" : $"{n} articles are") + " waiting in Needs review.\n" +
            "\n" +
            titles +
            more +
            "\n" +
            $"Review them: {Link("/articles?status=needs_review")}\n" +
            Footer());
    }

    private string Footer() => $"\nChange alerts in Settings: {Link("/settings")}\n";

    private string Link(string pathAndQuery)
    {
        var origin = options.Value.Origin ?? throw new InvalidOperationException("Missing required setting: Keryx__AdminApp__Origin");
        return origin.GetLeftPart(UriPartial.Authority) + pathAndQuery;
    }

    /// <summary>"Fri 27 Nov, 10:00 AM", Toronto time.</summary>
    internal static string When(DateTimeOffset at) =>
        TimeZoneInfo.ConvertTime(at, TorontoTime.Zone).ToString("ddd d MMM, h:mm tt", CultureInfo.InvariantCulture);

    /// <summary>The admin app's title for an issue's kind.</summary>
    internal static string KindTitle(IssueKind kind) => kind switch
    {
        IssueKind.SourceNotFound => "Feed returned 404",
        IssueKind.SourceTimeout => "Source timed out",
        IssueKind.SourceBlocked => "Source blocked Keryx",
        IssueKind.SourceInvalid => "Feed couldn't be read",
        _ => "Something needs attention",
    };

    internal static string OneLine(string text) => Whitespace().Replace(text, " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
