namespace Keryx.Domain.Staff;

public sealed class StaffSession(string id, string subjectId, byte[] protectedTicket, DateTimeOffset createdAt)
{
    public string Id { get; private set; } = id;
    public string SubjectId { get; private set; } = subjectId;
    public byte[] ProtectedTicket { get; private set; } = protectedTicket;
    public DateTimeOffset CreatedAt { get; private set; } = createdAt;
    public DateTimeOffset LastSeenAt { get; private set; } = createdAt;

    public bool IsExpired(DateTimeOffset now, TimeSpan idleLimit, TimeSpan absoluteLimit) =>
        now - LastSeenAt >= idleLimit || now - CreatedAt >= absoluteLimit;

    public void ReplaceTicket(byte[] protectedTicket, DateTimeOffset now)
    {
        ProtectedTicket = protectedTicket;
        CreatedAt = now;
        LastSeenAt = now;
    }

    public void Seen(DateTimeOffset now)
    {
        if (now > LastSeenAt)
        {
            LastSeenAt = now;
        }
    }
}
