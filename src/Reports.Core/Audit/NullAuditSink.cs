namespace Reports.Core.Audit;

/// <summary>Audit sink that drops every event, used where auditing is not wired.</summary>
public sealed class NullAuditSink : IAuditSink
{
    /// <summary>Shared instance.</summary>
    public static readonly NullAuditSink Instance = new();

    /// <inheritdoc/>
    public void Record(
        string action,
        AuditTarget target,
        string? targetId,
        AuditOutcome outcome,
        IReadOnlyDictionary<string, string>? details = null)
    {
    }
}
