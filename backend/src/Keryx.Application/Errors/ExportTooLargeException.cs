namespace Keryx.Application.Errors;

/// <summary>More articles match than one export holds (L2-054 AC3): staff narrow the filters.</summary>
public sealed class ExportTooLargeException() : KeryxException(400, "export_too_large", "Too many articles match to export.");
