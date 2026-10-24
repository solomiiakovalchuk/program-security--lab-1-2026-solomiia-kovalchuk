namespace Reports.Core.Audit;

/// <summary>Result of an audited action.</summary>
public enum AuditOutcome
{
    /// <summary>The action was performed.</summary>
    Success,

    /// <summary>The action was executed but failed.</summary>
    Failure,

    /// <summary>The action was blocked by the access policy.</summary>
    Denied
}
