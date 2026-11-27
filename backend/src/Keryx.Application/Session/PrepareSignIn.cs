using MediatR;

namespace Keryx.Application.Session;

public sealed record PrepareSignIn(string? ReturnPath) : IRequest<string>;

internal sealed class PrepareSignInHandler : IRequestHandler<PrepareSignIn, string>
{
    public Task<string> Handle(PrepareSignIn request, CancellationToken cancellationToken)
    {
        var path = request.ReturnPath;
        if (string.IsNullOrEmpty(path) || !path.StartsWith('/') || path.Any(char.IsControl) ||
            path.Contains("//", StringComparison.Ordinal) || path.Contains('\\') ||
            path.StartsWith("/sign-in", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult("/");
        }

        return Task.FromResult(path);
    }
}
