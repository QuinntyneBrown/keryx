namespace Keryx.Domain.Collection;

/// <summary>Whether a collected item is served, held for staff review, or hidden (L2-013).</summary>
public enum PublicationStatus
{
    Published,
    NeedsReview,
    Hidden,
}

/// <summary>
/// The status an item's score gives it: published at or above the threshold <c>T</c>, held for
/// review from the review floor 0.45 up to <c>T</c>, hidden below the floor. With <c>T</c> below
/// the floor nothing is held.
/// </summary>
public static class PublicationPolicy
{
    public const decimal ReviewFloor = 0.45m;

    public static PublicationStatus Classify(decimal score, decimal threshold) =>
        score >= threshold ? PublicationStatus.Published
        : score >= ReviewFloor ? PublicationStatus.NeedsReview
        : PublicationStatus.Hidden;
}
