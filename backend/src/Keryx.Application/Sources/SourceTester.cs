using Keryx.Application.Abstractions;
using Keryx.Application.Scoring;
using Keryx.Domain;
using Keryx.Domain.Sources;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Sources;

/// <summary>What a test fetch found (L2-002): numbers, and a reason from the fixed list when it failed.</summary>
public sealed record SourceTestResult(bool Reachable, string? Reason, int ItemsFound, int ItemsRelevant)
{
    internal static SourceTestResult Unreachable(FetchFailureReason reason) => new(false, Words(reason), 0, 0);

    public static string Words(FetchFailureReason reason) => reason switch
    {
        FetchFailureReason.NotFound => "not_found",
        FetchFailureReason.Timeout => "timeout",
        FetchFailureReason.BlockedByRobots => "blocked_by_robots",
        FetchFailureReason.Refused => "refused",
        FetchFailureReason.InvalidContent => "invalid_content",
        FetchFailureReason.UnsafeAddress => "unsafe_address",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown failure reason."),
    };
}

/// <summary>
/// Fetches a source once, outside a run, and says whether it works and how much of it looks
/// relevant (L2-002). It writes nothing. An item counts as relevant at or above the publish
/// threshold, as a run would publish it. Instagram and Eventbrite are read only through their
/// official APIs; with no credentials configured they are refused without a request.
/// </summary>
public sealed class SourceTester(
    IAppDbContext db,
    SafeAddressCheck safety,
    ISourceFetcher fetcher,
    IFeedParser parser,
    IRelevanceScorer scorer)
{
    public async Task<SourceTestResult> TestAsync(SourceKind kind, Uri address, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (kind is SourceKind.Instagram or SourceKind.Eventbrite)
        {
            return SourceTestResult.Unreachable(FetchFailureReason.Refused);
        }

        if (!await safety.IsSafeAsync(address, cancellationToken).ConfigureAwait(false))
        {
            return SourceTestResult.Unreachable(FetchFailureReason.UnsafeAddress);
        }

        var settings = await db.Settings.AsNoTracking().SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            ?? Domain.Seeding.SeedData.DefaultSettings();
        var fetched = await fetcher.FetchAsync(
            new SourceFetchRequest(kind, address, TimeSpan.FromSeconds(settings.TimeoutSeconds)), cancellationToken).ConfigureAwait(false);
        if (!fetched.Succeeded)
        {
            return SourceTestResult.Unreachable(fetched.Failure ?? FetchFailureReason.Refused);
        }

        if (fetched.NotModified)
        {
            return new SourceTestResult(true, null, 0, 0);
        }

        var feed = parser.Parse(kind, fetched.Content);
        if (feed is null)
        {
            return SourceTestResult.Unreachable(FetchFailureReason.InvalidContent);
        }

        var scoring = ScoringSettings.From(settings);
        var relevant = feed.Items.Count(item =>
            scorer.Score(new ScoringItem(item.Title, item.Summary, string.Empty), scoring).Value >= settings.PublishThreshold);
        return new SourceTestResult(true, null, feed.Items.Count, relevant);
    }
}
