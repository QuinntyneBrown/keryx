using FluentValidation;
using Keryx.Application.Abstractions;
using Keryx.Application.Audit;
using Keryx.Domain.Audit;
using Keryx.Domain.Sources;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Sources;

/// <summary>Changes a source's address, organization and region (L2-003 AC1); its kind stays.</summary>
public sealed record UpdateSource(string Id, string? Address, string? OrganizationName, string? Region) : IRequest<SourceSaved>;

/// <summary>The saved source and one test fetch of its saved address (L2-003 AC1).</summary>
public sealed record SourceSaved(SourceDto Source, SourceTestResult Test);

/// <summary>
/// The rules of adding a source (L2-001 AC2). A news query's address is its query text, so it
/// need only be given; any other kind's address is a public URL.
/// </summary>
internal sealed class UpdateSourceValidator : AbstractValidator<UpdateSource>
{
    private const int QueryLength = 2048;

    public UpdateSourceValidator(IAppDbContext db, SafeAddressCheck safety)
    {
        WhenAsync((request, cancellationToken) => IsNewsQueryAsync(db, request.Id, cancellationToken), () =>
            RuleFor(request => request.Address)
                .Must(query => !string.IsNullOrWhiteSpace(query) && query.Length <= QueryLength)
                .WithMessage("Enter the query text."))
            .Otherwise(() => RuleFor(request => request.Address).SafeUrl(safety));
        RuleFor(request => request.OrganizationName).OrganizationName();
        RuleFor(request => request.Region).KnownRegion(db);
    }

    private static Task<bool> IsNewsQueryAsync(IAppDbContext db, string id, CancellationToken cancellationToken) =>
        db.Sources.AnyAsync(source => source.PublicId == id && source.Kind == SourceKind.NewsQuery, cancellationToken);
}

/// <summary>
/// Saves the change unless it would equal another source of the kind (L2-003 AC2), then tests the
/// saved address once. The test leaves the stored health as it is, as a test fetch does (L2-002).
/// </summary>
internal sealed class UpdateSourceHandler(IAppDbContext db, IAuditTrail audit, SourceTester tester) : IRequestHandler<UpdateSource, SourceSaved>
{
    public async Task<SourceSaved> Handle(UpdateSource request, CancellationToken cancellationToken)
    {
        var source = await db.Sources
            .SingleOrDefaultAsync(candidate => candidate.PublicId == request.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new SourceNotFoundException();
        var normalized = CanonicalUrl.Normalize(request.Address!);
        if (await db.Sources.AnyAsync(
            other => other.Id != source.Id && other.Kind == source.Kind && other.NormalizedAddress == normalized,
            cancellationToken).ConfigureAwait(false))
        {
            throw new SourceExistsException();
        }

        source.Update(request.Address!, request.OrganizationName!, request.Region!);
        audit.Record(AuditAction.SourceEdited, source.PublicId);
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // Another request took the address between the check and the save (L2-001 AC3).
            throw new SourceExistsException();
        }

        var test = await tester.TestStoredAsync(source, cancellationToken).ConfigureAwait(false);
        return new SourceSaved(await SourceDto.ReadAsync(db, source, cancellationToken).ConfigureAwait(false), test);
    }
}
