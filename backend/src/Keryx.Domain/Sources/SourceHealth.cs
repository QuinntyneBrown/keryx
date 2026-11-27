namespace Keryx.Domain.Sources;

/// <summary>How a source's recent fetches went (L2-004); derived from fetches, never typed by a person.</summary>
public enum SourceHealth
{
    Healthy,
    NeedsLook,
    Failing,
}
