using System.Globalization;
using Keryx.Application.Abstractions;
using Keryx.Application.Errors;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Session;

public sealed record GetCurrentStaff(string? SubjectId) : IRequest<StaffProfile>;

public sealed record StaffProfile(string Name, string Initials, string Role);

internal sealed class GetCurrentStaffHandler(IAppDbContext db) : IRequestHandler<GetCurrentStaff, StaffProfile>
{
    public async Task<StaffProfile> Handle(GetCurrentStaff request, CancellationToken cancellationToken)
    {
        var member = await db.StaffMembers.AsNoTracking().SingleOrDefaultAsync(
            person => person.SubjectId == request.SubjectId, cancellationToken).ConfigureAwait(false)
            ?? throw new UnknownStaffException();
        var words = member.Name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var initials = words.Length switch
        {
            0 => string.Empty,
            1 => StringInfo.GetNextTextElement(words[0]),
            _ => StringInfo.GetNextTextElement(words[0]) + StringInfo.GetNextTextElement(words[^1]),
        };
        return new StaffProfile(member.Name, initials.ToUpperInvariant(), member.Role);
    }

    private sealed class UnknownStaffException() : KeryxException(403, "not_staff", "Staff access is required.");
}
