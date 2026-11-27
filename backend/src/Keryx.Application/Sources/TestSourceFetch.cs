using FluentValidation;
using Keryx.Application.Abstractions;
using Keryx.Domain.Sources;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Sources;

/// <summary>Test-fetches an address before it is added (L2-002 AC1, AC2).</summary>
public sealed record TestSourceFetch(string? Kind, string? Address) : IRequest<SourceTestResult>;

internal sealed class TestSourceFetchValidator : AbstractValidator<TestSourceFetch>
{
    public TestSourceFetchValidator()
    {
        RuleFor(request => request.Kind)
            .Must(kind => SourceWords.KindOf(kind) is { } known && known != SourceKind.NewsQuery)
            .WithMessage("Choose a kind: rss, website, instagram or eventbrite.");
        RuleFor(request => request.Address)
            .Must(SourceAddress.IsAcceptable)
            .WithMessage("Enter a full URL starting with http:// or https://, with no user name, password or port.");
    }
}

internal sealed class TestSourceFetchHandler(SourceTester tester) : IRequestHandler<TestSourceFetch, SourceTestResult>
{
    public Task<SourceTestResult> Handle(TestSourceFetch request, CancellationToken cancellationToken) =>
        tester.TestAsync(SourceWords.KindOf(request.Kind)!.Value, new Uri(request.Address!), cancellationToken);
}

/// <summary>Test-fetches a source in the catalogue; its stored health is not changed (L2-002 AC3).</summary>
public sealed record TestExistingSourceFetch(string Id) : IRequest<SourceTestResult>;

internal sealed class TestExistingSourceFetchHandler(IAppDbContext db, SourceTester tester)
    : IRequestHandler<TestExistingSourceFetch, SourceTestResult>
{
    public async Task<SourceTestResult> Handle(TestExistingSourceFetch request, CancellationToken cancellationToken)
    {
        var source = await db.Sources.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.PublicId == request.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new SourceNotFoundException();
        return Uri.TryCreate(source.Address, UriKind.Absolute, out var address)
            ? await tester.TestAsync(source.Kind, address, cancellationToken).ConfigureAwait(false)
            : SourceTestResult.Unreachable(FetchFailureReason.Refused);
    }
}
