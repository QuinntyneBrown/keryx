using Keryx.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Sources;

/// <summary>Stops checking a source and keeps every record collected from it (L2-003 AC3).</summary>
public sealed record RemoveSource(string Id) : IRequest;

/// <summary>
/// Marks the source removed, so nothing fetches, lists or matches it, and closes its open and
/// muted issues: no run will raise or clear them again, so they would count forever.
/// </summary>
internal sealed class RemoveSourceHandler(IAppDbContext db, TimeProvider time) : IRequestHandler<RemoveSource>
{
    public async Task Handle(RemoveSource request, CancellationToken cancellationToken)
    {
        var source = await db.Sources
            .SingleOrDefaultAsync(candidate => candidate.PublicId == request.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new SourceNotFoundException();
        source.Remove(time.GetUtcNow());
        var issues = await db.RunIssues.Where(issue => issue.SourceId == source.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var issue in issues)
        {
            issue.Close();
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Undo of a removal: the same source, active again (L2-065 AC4).</summary>
public sealed record RestoreSource(string Id) : IRequest<SourceDto>;

/// <summary>
/// Restores the source unless an active source of its kind and address was added meanwhile, which
/// is refused with <c>source_exists</c> (L2-001 AC3). Its closed issues stay closed; the next run
/// raises any that still apply.
/// </summary>
internal sealed class RestoreSourceHandler(IAppDbContext db) : IRequestHandler<RestoreSource, SourceDto>
{
    public async Task<SourceDto> Handle(RestoreSource request, CancellationToken cancellationToken)
    {
        var source = await db.Sources.IgnoreQueryFilters()
            .SingleOrDefaultAsync(candidate => candidate.PublicId == request.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new SourceNotFoundException();
        if (await db.Sources.AnyAsync(
            other => other.Id != source.Id && other.Kind == source.Kind && other.NormalizedAddress == source.NormalizedAddress,
            cancellationToken).ConfigureAwait(false))
        {
            throw new SourceExistsException();
        }

        source.Restore();
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // The same source was added between the check and the save.
            throw new SourceExistsException();
        }

        return await SourceDto.ReadAsync(db, source, cancellationToken).ConfigureAwait(false);
    }
}
