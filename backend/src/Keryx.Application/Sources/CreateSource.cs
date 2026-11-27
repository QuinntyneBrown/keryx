using Keryx.Application.Abstractions;
using Keryx.Domain.Sources;
using MediatR;

namespace Keryx.Application.Sources;

/// <summary>Adds a source to the catalogue (L2-001); the next collection run fetches it.</summary>
public sealed record CreateSource(string? Kind, string? Address, string? OrganizationName, string? Region) : IRequest<SourceDto>;

internal sealed class CreateSourceHandler(IAppDbContext db, TimeProvider time) : IRequestHandler<CreateSource, SourceDto>
{
    public async Task<SourceDto> Handle(CreateSource request, CancellationToken cancellationToken)
    {
        var source = Source.Add(
            SourceWords.KindOf(request.Kind)!.Value,
            request.Address!,
            request.OrganizationName!,
            request.Region!,
            time.GetUtcNow());
        db.Sources.Add(source);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return SourceDto.From(source);
    }
}
