using Reports.Core.Audit;

namespace Reports.Identity.Tests;

/// <summary>
/// Audit sink that keeps every recorded event for assertions.
/// </summary>
internal sealed class CapturingAuditSink : IAuditSink
{
    /// <summary>
    /// Gets the recorded events in order.
    /// </summary>
    public List<Event> Events { get; } = new();

    /// <inheritdoc/>
    public void Record(
        string action,
        AuditTarget target,
        string? targetId,
        AuditOutcome outcome,
        IReadOnlyDictionary<string, string>? details = null)
    {
        Events.Add(new Event(action, target, targetId, outcome, details ?? new Dictionary<string, string>()));
    }

    /// <summary>
    /// One captured event.
    /// </summary>
    internal sealed record Event(
        string Action,
        AuditTarget Target,
        string? TargetId,
        AuditOutcome Outcome,
        IReadOnlyDictionary<string, string> Details);
}
