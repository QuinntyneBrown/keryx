using Keryx.Application.Abstractions;
using Keryx.Application.Errors;
using Keryx.Domain.Sources;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Sources;

/// <summary>Adds a source to the catalogue (L2-001); the next collection run fetches it.</summary>
public sealed record CreateSource(string? Kind, string? Address, string? OrganizationName, string? Region) : IRequest<SourceDto>;

internal sealed class CreateSourceHandler(IAppDbContext db, TimeProvider time) : IRequestHandler<CreateSource, SourceDto>
{
    public async Task<SourceDto> Handle(CreateSource request, CancellationToken cancellationToken)
    {
        var kind = SourceWords.KindOf(request.Kind)!.Value;
        if (kind == SourceKind.NewsQuery)
        {
            throw new KindNotCreatableException();
        }

        var source = Source.Add(
            kind,
            request.Address!,
            request.OrganizationName!,
            request.Region!,
            time.GetUtcNow());
        if (await db.Sources.AnyAsync(
            existing => existing.Kind == source.Kind && existing.NormalizedAddress == source.NormalizedAddress,
            cancellationToken).ConfigureAwait(false))
        {
            throw new SourceExistsException();
        }

        db.Sources.Add(source);
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // Another request added the same source between the check and the save: the unique
            // index on kind and normalized address refused this one.
            throw new SourceExistsException();
        }

        return SourceDto.From(source);
    }
}

/// <summary>News query sources come from seed data only (L2-001 AC5).</summary>
public sealed class KindNotCreatableException()
    : KeryxException(400, "kind_not_creatable", "Sources of this kind are created by seed data only.");

/// <summary>A source of the same kind and normalized address is already in the catalogue (L2-001 AC3).</summary>
public sealed class SourceExistsException()
    : KeryxException(409, "source_exists", "The source is already in the catalogue.");
