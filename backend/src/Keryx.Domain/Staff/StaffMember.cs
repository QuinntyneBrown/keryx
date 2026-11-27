namespace Keryx.Domain.Staff;

/// <summary>The provider subject and the display fields used by the staff console.</summary>
public sealed class StaffMember(string subjectId, string name, string role)
{
    public string SubjectId { get; private set; } = subjectId;
    public string Name { get; private set; } = name;
    public string Role { get; private set; } = role;
}
