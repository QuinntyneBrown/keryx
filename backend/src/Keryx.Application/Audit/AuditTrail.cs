using Keryx.Application.Abstractions;
using Keryx.Domain.Audit;

namespace Keryx.Application.Audit;

/// <summary>The signed-in staff member, as the identity provider named them.</summary>
public interface ICurrentStaff
{
    /// <summary>The <c>sub</c> claim of the staff session.</summary>
    string SubjectId { get; }
}

/// <summary>
/// Records a staff action for audit (L2-085). A handler calls it just before its own save, so the
/// entry is committed with the change or not at all; it saves nothing itself.
/// </summary>
public interface IAuditTrail
{
    void Record(AuditAction action, string targetId, IReadOnlyList<string>? changedFields = null);
}

internal sealed class AuditTrail(IAppDbContext db, ICurrentStaff staff, TimeProvider time) : IAuditTrail
{
    public void Record(AuditAction action, string targetId, IReadOnlyList<string>? changedFields = null) =>
        db.AuditEntries.Add(AuditEntry.Of(staff.SubjectId, action, targetId, changedFields, time.GetUtcNow()));
}
