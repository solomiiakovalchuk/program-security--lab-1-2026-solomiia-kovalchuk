namespace Reports.Core.Audit;

/// <summary>Kind of object an audit event refers to.</summary>
public enum AuditTarget
{
    Account,
    Mission,
    ReferenceItem,
    Person,
    Settings,
    Report,
    GoogleSync,
    Audit,
    Application
}
