namespace Keryx.Application.Scoring;

/// <summary>
/// A relevance score from 0.00 to 1.00, to two decimal places (L2-012 AC4). It is a decimal,
/// rounded half away from zero, because a binary double holds 0.585 as slightly less and would
/// round it down to 0.58.
/// </summary>
public readonly record struct RelevanceScore
{
    private RelevanceScore(decimal value) => Value = value;

    public decimal Value { get; }

    public static RelevanceScore Of(decimal raw) =>
        new(Math.Round(Math.Clamp(raw, 0m, 1m), 2, MidpointRounding.AwayFromZero));

    public override string ToString() => Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
}
