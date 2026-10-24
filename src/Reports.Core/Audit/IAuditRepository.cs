namespace Reports.Core.Audit;

/// <summary>Append-only storage of audit events; the only deletion is retention.</summary>
public interface IAuditRepository
{
    /// <summary>Appends one event.</summary>
    Task AppendAsync(AuditEvent auditEvent, CancellationToken ct = default);

    /// <summary>Returns a page of events ordered by time descending, then by id descending.</summary>
    Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditQuery query, CancellationToken ct = default);

    /// <summary>Counts events matching the filter, ignoring Skip and Take.</summary>
    Task<int> CountAsync(AuditQuery query, CancellationToken ct = default);

    /// <summary>Returns up to <paramref name="take"/> events with an id above <paramref name="afterId"/>, oldest first.</summary>
    Task<IReadOnlyList<AuditEvent>> GetOldestFirstAsync(long afterId, int take, CancellationToken ct = default);

    /// <summary>Returns the newest event in chain order, or null when the log is empty.</summary>
    Task<AuditEvent?> GetLastAsync(CancellationToken ct = default);

    /// <summary>Deletes events older than the cutoff; used only by retention.</summary>
    Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken ct = default);
}
