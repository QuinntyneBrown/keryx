using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Keryx.Api.Tests.Probes;

/// <summary>
/// Endpoints that exist only in the tests' host, to reach the parts of the pipeline every real
/// endpoint stands on before any real endpoint exists: validation and the unhandled failure.
/// </summary>
[ApiController]
[Route("test/probe")]
public sealed class ProbeController(ISender sender) : ControllerBase
{
    /// <summary>What an unhandled failure might carry: none of it may reach a response.</summary>
    public const string SecretDetail = "Host=db.internal;Password=hunter2 in /srv/keryx/Program.cs";

    [HttpGet("throw")]
    public IActionResult Throw() => throw new InvalidOperationException($"{SecretDetail} ({Request.Method})");

    [HttpPost]
    public async Task<ProbeReply> Post(ProbeRequest body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        return await sender.Send(new ProbeCommand(body.DisplayName), cancellationToken).ConfigureAwait(false);
    }
}

public sealed record ProbeRequest(string? DisplayName);

public sealed record ProbeReply(string DisplayName, int HandledCount);

public sealed record ProbeCommand(string? DisplayName) : IRequest<ProbeReply>;

/// <summary>How many times the probe's handler ran, in this fixture.</summary>
public sealed class ProbeCounter
{
    private int _handled;

    public int Handled => _handled;

    public int Increment() => Interlocked.Increment(ref _handled);
}

internal sealed class ProbeCommandValidator : AbstractValidator<ProbeCommand>
{
    public ProbeCommandValidator()
    {
        RuleFor(command => command.DisplayName).NotEmpty().MaximumLength(5);
    }
}

internal sealed class ProbeCommandHandler(ProbeCounter counter) : IRequestHandler<ProbeCommand, ProbeReply>
{
    public Task<ProbeReply> Handle(ProbeCommand request, CancellationToken cancellationToken) =>
        Task.FromResult(new ProbeReply(request.DisplayName!, counter.Increment()));
}
