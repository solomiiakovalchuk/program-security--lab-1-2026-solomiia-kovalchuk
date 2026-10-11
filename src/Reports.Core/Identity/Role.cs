namespace Reports.Core.Identity;

/// <summary>Application roles.</summary>
public enum Role
{
    /// <summary>Manages accounts, settings, reference data and the audit log.</summary>
    Administrator,

    /// <summary>Works with reports, the journal, sync and records.</summary>
    Operator
}
