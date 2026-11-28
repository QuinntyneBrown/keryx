namespace Keryx.Application.Errors;

/// <summary>
/// A refusal a handler makes on purpose. It carries what the API answers with: the HTTP status,
/// the <c>code</c> a client branches on and a generic title. It never carries a value a caller
/// sent or a detail of the system.
/// </summary>
public abstract class KeryxException : Exception
{
    protected KeryxException(int status, string code, string title)
        : base(title)
    {
        Status = status;
        Code = code;
        Title = title;
    }

    public int Status { get; }

    public string Code { get; }

    public string Title { get; }

    /// <summary>
    /// Values a client acts on beside the code, as ProblemDetails extensions (the running run's
    /// <c>run_id</c>); ids the API itself made, never a value a caller sent.
    /// </summary>
    public virtual IReadOnlyDictionary<string, object?> Extensions { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);
}
