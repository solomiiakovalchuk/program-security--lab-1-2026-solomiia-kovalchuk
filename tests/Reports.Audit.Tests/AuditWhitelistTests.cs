using Reports.Audit;
using Reports.Core.Audit;

namespace Reports.Audit.Tests;

/// <summary>Keys that the services really emit must survive the details whitelist.</summary>
public class AuditWhitelistTests
{
    private static string Run(string action, params (string Key, string Value)[] pairs) =>
        AuditDetailsSanitizer.Sanitize(action, pairs.ToDictionary(p => p.Key, p => p.Value));

    [Fact]
    public void LoginFailed_KeepsLoginKnownAndReason()
    {
        var text = Run(AuditAction.LoginFailed, ("loginKnown", "false"), ("reason", "BadPassword"));
        Assert.Contains("loginKnown=false", text);
        Assert.Contains("reason=BadPassword", text);
    }

    [Fact]
    public void AccountLockedOut_KeepsLockedUntilUtc()
    {
        Assert.Contains("lockedUntilUtc=", Run(AuditAction.AccountLockedOut, ("lockedUntilUtc", "2026-11-04T10:00:00.0000000Z")));
    }

    [Fact]
    public void PasswordChanged_KeepsInitiator()
    {
        Assert.Contains("initiator=Self", Run(AuditAction.PasswordChanged, ("initiator", "Self")));
    }

    [Fact]
    public void SpreadsheetChanged_KeepsBothIds()
    {
        var text = Run(AuditAction.GoogleSpreadsheetChanged, ("fromId", "AAA"), ("toId", "BBB"));
        Assert.Contains("fromId=AAA", text);
        Assert.Contains("toId=BBB", text);
    }

    [Fact]
    public void SecretLookingKeys_AreNeverStored()
    {
        var text = Run(AuditAction.LoginFailed, ("password", "hunter2"), ("passwordHash", "x"), ("reason", "BadPassword"));
        Assert.DoesNotContain("hunter2", text);
        Assert.DoesNotContain("passwordHash", text);
        Assert.Contains("reason=BadPassword", text);
    }

    [Fact]
    public void DataChangingActions_KeepTheKeysTheServicesEmit()
    {
        Assert.Contains("file=r.docx", Run(AuditAction.ReportGenerated, ("file", "r.docx"), ("period", "a"), ("count", "3")));
        Assert.Contains("count=3", Run(AuditAction.ReportGenerated, ("file", "r.docx"), ("period", "a"), ("count", "3")));
        Assert.Contains("added=2", Run(AuditAction.SyncImport, ("added", "2"), ("updated", "0"), ("skipped", "1"), ("total", "3")));
        Assert.Contains("added=2", Run(AuditAction.GoogleSyncCompleted, ("added", "2")));
        Assert.Contains("kind=БпЛА", Run(AuditAction.ReferenceAdded, ("kind", "БпЛА"), ("name", "FPV")));
        Assert.Contains("email=a@b.c", Run(AuditAction.PersonUpdated, ("email", "a@b.c")));
        Assert.Contains("rows=4", Run(AuditAction.GooglePublishCompleted, ("rows", "4")));
        Assert.Contains("result=usable", Run(AuditAction.GoogleConnectionChecked, ("result", "usable")));
        Assert.Contains("fields=OutputFolder,FontSize", Run(AuditAction.SettingsChanged, ("fields", "OutputFolder,FontSize")));
        Assert.Contains("action=Sync", Run(AuditAction.AccessDenied, ("action", "Sync"), ("role", "Operator")));
        Assert.Contains("version=1.0", Run(AuditAction.ApplicationStarted, ("version", "1.0")));
        Assert.Contains("reason=file modified", Run(AuditAction.IntegrityCheckFailed, ("reason", "file modified"), ("count", "1")));
        Assert.Contains("reason=no manifest", Run(AuditAction.IntegrityUnsigned, ("reason", "no manifest")));
    }
}
