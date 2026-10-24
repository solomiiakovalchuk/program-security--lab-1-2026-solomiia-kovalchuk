namespace Reports.Core.Audit;

/// <summary>One immutable audit log record; the hash fields link it to the previous record.</summary>
public sealed class AuditEvent
{
    /// <summary>Database sequence number; defines the order of the hash chain.</summary>
    public long Id { get; init; }

    /// <summary>Unique identifier of the event.</summary>
    public Guid EventUuid { get; init; }

    /// <summary>UTC time set by the recording service.</summary>
    public DateTime OccurredAtUtc { get; init; }

    /// <summary>Account of the actor, or null for system and anonymous actors.</summary>
    public Guid? ActorAccountId { get; init; }

    /// <summary>Login of the actor at event time (snapshot).</summary>
    public string ActorLogin { get; init; } = string.Empty;

    /// <summary>Event type, a key from <see cref="AuditAction"/>.</summary>
    public string ActionType { get; init; } = string.Empty;

    /// <summary>Kind of the object the event refers to.</summary>
    public AuditTarget TargetType { get; init; }

    /// <summary>Identifier of the object, never its content.</summary>
    public string? TargetId { get; init; }

    /// <summary>Result of the action.</summary>
    public AuditOutcome Outcome { get; init; }

    /// <summary>Sanitized description, at most 2000 characters.</summary>
    public string Details { get; init; } = string.Empty;

    /// <summary>Operating system user name.</summary>
    public string OsUser { get; init; } = string.Empty;

    /// <summary>Name of the machine.</summary>
    public string MachineName { get; init; } = string.Empty;

    /// <summary>Hash of the previous record, empty for the first one.</summary>
    public string PrevHash { get; init; } = string.Empty;

    /// <summary>SHA-256 (hex) of the previous hash and this record's content.</summary>
    public string Hash { get; init; } = string.Empty;
}
