namespace Reports.Core.Audit;

/// <summary>Registry of audit event types; keys are stable and never reused.</summary>
public static class AuditAction
{
    public const string LoginSucceeded = "Auth.LoginSucceeded";
    public const string LoginFailed = "Auth.LoginFailed";
    public const string AccountLockedOut = "Auth.AccountLockedOut";
    public const string Logout = "Auth.Logout";

    public const string PasswordChanged = "Password.Changed";
    public const string PasswordResetByAdmin = "Password.ResetByAdmin";

    public const string AccountCreated = "Account.Created";
    public const string AccountDeleted = "Account.Deleted";
    public const string AccountBlocked = "Account.Blocked";
    public const string AccountUnblocked = "Account.Unblocked";
    public const string AccountRoleChanged = "Account.RoleChanged";

    public const string AccessDenied = "Access.Denied";

    public const string ReportGenerated = "Report.Generated";
    public const string MissionCancelled = "Mission.Cancelled";
    public const string MissionRestored = "Mission.Restored";
    public const string SyncImport = "Sync.Import";

    public const string ReferenceAdded = "Reference.Added";
    public const string ReferenceUpdated = "Reference.Updated";
    public const string ReferenceDeleted = "Reference.Deleted";
    public const string PersonAdded = "Person.Added";
    public const string PersonUpdated = "Person.Updated";
    public const string PersonDeleted = "Person.Deleted";

    public const string GoogleSyncCompleted = "Google.SyncCompleted";
    public const string GooglePublishCompleted = "Google.PublishCompleted";
    public const string GoogleTokenForgotten = "Google.TokenForgotten";
    public const string GoogleConnectionChecked = "Google.ConnectionChecked";
    public const string GoogleSpreadsheetChanged = "Google.SpreadsheetChanged";

    public const string SettingsChanged = "Settings.Changed";
    public const string TemplateRebuilt = "Template.Rebuilt";

    public const string AuditExported = "Audit.Exported";
    public const string AuditRetentionApplied = "Audit.RetentionApplied";

    public const string ApplicationStarted = "App.Started";
    public const string IntegrityCheckFailed = "App.IntegrityCheckFailed";
    public const string IntegrityUnsigned = "App.IntegrityUnsigned";
}
