namespace Keryx.Domain.Collection;

/// <summary>What a run did to the records and how its sources answered, every one a number (L2-019).</summary>
public sealed record RunCounts
{
    public int Added { get; init; }

    public int Updated { get; init; }

    public int Unchanged { get; init; }

    public int Duplicates { get; init; }

    public int Held { get; init; }

    public int OffTopic { get; init; }

    public int Rejected { get; init; }

    public int SourcesOk { get; init; }

    public int SourcesFailed { get; init; }
}
