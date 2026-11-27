using FluentValidation.Results;

namespace Keryx.Application.Errors;

/// <summary>
/// A request a validator refused: <c>400</c>, with an entry for each failing field. The code is
/// the one the first failing rule set, or <c>validation_failed</c> when none did.
/// </summary>
public sealed class RequestValidationException : KeryxException
{
    private const int BadRequest = 400;

    public RequestValidationException(IReadOnlyCollection<ValidationFailure> failures)
        : base(BadRequest, CodeOf(failures), "The request is not valid.")
    {
        Errors = failures
            .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
    }

    /// <summary>Each failing property's messages, keyed by the property's name as the handler sees it.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    /// <summary>
    /// FluentValidation names a rule's code after its validator (<c>NotEmptyValidator</c>) unless
    /// the rule sets one with <c>WithErrorCode</c>; only a code a rule set is a client's to branch on.
    /// </summary>
    private static string CodeOf(IEnumerable<ValidationFailure> failures) =>
        failures
            .Select(failure => failure.ErrorCode)
            .FirstOrDefault(code => !string.IsNullOrEmpty(code) && !code.EndsWith("Validator", StringComparison.Ordinal))
        ?? ErrorCodes.ValidationFailed;
}
