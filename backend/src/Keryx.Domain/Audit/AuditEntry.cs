namespace Keryx.Domain.Audit;

/// <summary>A state-changing staff action (L2-085).</summary>
public enum AuditAction
{
    ArticlePublished,
    ArticleHidden,
    ArticleReverted,
    SourceAdded,
    SourceEdited,
    SourceRemoved,
    SourceRestored,
    ApiKeyCreated,
    ApiKeyRevoked,
    IssueMuted,
    IssueUnmuted,
    RunStarted,
    RunRetried,
    SettingsSaved,
}

/// <summary>
/// One successful state-changing staff action (L2-085): who (the identity provider's subject id),
/// what, on which record, and when. It has no field for a key, an e-mail address or source text;
/// for Settings it holds the names of the fields changed, never their values.
/// </summary>
public sealed class AuditEntry
{
    private AuditEntry()
    {
    }

    public long Id { get; private set; }

    public string SubjectId { get; private set; } = string.Empty;

    public AuditAction Action { get; private set; }

    /// <summary>The target's public id (<c>art_…</c>, <c>src_…</c>, <c>cli_…</c>, <c>run_…</c>, an issue's id), or <c>settings</c>.</summary>
    public string TargetId { get; private set; } = string.Empty;

    public IReadOnlyList<string> ChangedFields { get; private set; } = [];

    public DateTimeOffset OccurredAt { get; private set; }

    public static AuditEntry Of(string subjectId, AuditAction action, string targetId, IReadOnlyList<string>? changedFields, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrEmpty(subjectId);
        ArgumentException.ThrowIfNullOrEmpty(targetId);
        return new AuditEntry
        {
            SubjectId = subjectId,
            Action = action,
            TargetId = targetId,
            ChangedFields = changedFields ?? [],
            OccurredAt = now.ToUniversalTime(),
        };
    }
}
