namespace Keryx.Application.Errors;

/// <summary>
/// The <c>code</c> of an error a client branches on. These three belong to every endpoint; a
/// feature adds its own beside its handlers.
/// </summary>
public static class ErrorCodes
{
    /// <summary>An unhandled failure. The body says nothing more.</summary>
    public const string InternalError = "internal_error";

    /// <summary>No such path.</summary>
    public const string NotFound = "not_found";

    /// <summary>A request a validator refused; the body names each failing field.</summary>
    public const string ValidationFailed = "validation_failed";
}
