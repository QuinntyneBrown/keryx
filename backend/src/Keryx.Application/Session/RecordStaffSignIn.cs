using Keryx.Application.Abstractions;
using Keryx.Domain.Staff;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Session;

public sealed record RecordStaffSignIn(string SubjectId, string Name, string Role) : IRequest;

internal sealed class RecordStaffSignInHandler(IAppDbContext db) : IRequestHandler<RecordStaffSignIn>
{
    public async Task Handle(RecordStaffSignIn request, CancellationToken cancellationToken)
    {
        var member = await db.StaffMembers.FindAsync([request.SubjectId], cancellationToken).ConfigureAwait(false);
        if (member is null)
        {
            member = new StaffMember(request.SubjectId, request.Name, request.Role);
            db.StaffMembers.Add(member);
            try
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (DbUpdateException)
            {
                // Two callbacks can be the first sign-in for this subject.
                db.StaffMembers.Entry(member).State = EntityState.Detached;
                member = await db.StaffMembers.FindAsync([request.SubjectId], cancellationToken).ConfigureAwait(false);
                if (member is null)
                {
                    throw;
                }
            }
        }

        member.SetDisplay(request.Name, request.Role);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
