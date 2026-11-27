using FluentValidation;
using FluentValidation.Results;
using Keryx.Application.Errors;
using MediatR;

namespace Keryx.Application.Behaviors;

/// <summary>
/// Runs every validator of a request before its handler. Any failure throws
/// <see cref="RequestValidationException"/>, so the handler is never reached and nothing changes.
/// </summary>
internal sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);
        var failures = new List<ValidationFailure>();
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            failures.AddRange(result.Errors);
        }

        if (failures.Count > 0)
        {
            throw new RequestValidationException(failures);
        }

        return await next(cancellationToken).ConfigureAwait(false);
    }
}
