using Keryx.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.AdminSettings;

/// <summary>The one settings row and its version (L2-070 AC1).</summary>
public sealed record GetSettings : IRequest<SettingsDto>;

internal sealed class GetSettingsHandler(IAppDbContext db) : IRequestHandler<GetSettings, SettingsDto>
{
    public async Task<SettingsDto> Handle(GetSettings request, CancellationToken cancellationToken) =>
        SettingsDto.From(await db.Settings.AsNoTracking().SingleAsync(cancellationToken).ConfigureAwait(false));
}
