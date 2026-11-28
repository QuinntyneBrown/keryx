using Keryx.Application.Abstractions;
using Keryx.Application.Errors;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Sources;

/// <summary>One source by its public id.</summary>
public sealed record GetSource(string Id) : IRequest<SourceDto>;

internal sealed class GetSourceHandler(IAppDbContext db) : IRequestHandler<GetSource, SourceDto>
{
    public async Task<SourceDto> Handle(GetSource request, CancellationToken cancellationToken)
    {
        var source = await db.Sources.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.PublicId == request.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new SourceNotFoundException();
        return await SourceDto.ReadAsync(db, source, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>No source has the id asked for (L2-002 AC3).</summary>
public sealed class SourceNotFoundException() : KeryxException(404, "source_not_found", "The source was not found.");
