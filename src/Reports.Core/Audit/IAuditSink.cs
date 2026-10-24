namespace Reports.Core.Audit;

/// <summary>Receives audit events; implementations must never throw into the caller.</summary>
public interface IAuditSink
{
    /// <summary>Records one event; details must come from a whitelist and never hold secrets.</summary>
    void Record(
        string action,
        AuditTarget target,
        string? targetId,
        AuditOutcome outcome,
        IReadOnlyDictionary<string, string>? details = null);
}
