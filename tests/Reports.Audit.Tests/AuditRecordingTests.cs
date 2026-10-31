using Microsoft.Data.Sqlite;
using Reports.Core.Audit;
using Reports.Core.Identity;

namespace Reports.Audit.Tests;

public class AuditRecordingTests
{
    private static AuditQuery All => new(take: 100);

    [Fact]
    public async Task Record_StoresUtcTimeActorSnapshotAndHost()
    {
        using var f = new AuditFixture();
        var id = Guid.NewGuid();
        f.Session.Current = new Session(id, "Alice", Role.Operator);
        f.Add(AuditAction.ReportGenerated);

        var e = Assert.Single(await f.Repository.QueryAsync(All));
        Assert.Equal(DateTimeKind.Utc, e.OccurredAtUtc.Kind);
        Assert.Equal(f.Clock.UtcNow, e.OccurredAtUtc);
        Assert.Equal(id, e.ActorAccountId);
        Assert.Equal("Alice", e.ActorLogin);
        Assert.Equal("tester", e.OsUser);
        Assert.Equal("TESTPC", e.MachineName);
        Assert.NotEqual(Guid.Empty, e.EventUuid);
    }

    [Fact]
    public async Task Record_BeforeLogin_UsesAnonymousActor()
    {
        using var f = new AuditFixture();
        f.Add(AuditAction.LoginFailed);

        var e = Assert.Single(await f.Repository.QueryAsync(All));
        Assert.Equal("anonymous", e.ActorLogin);
        Assert.Null(e.ActorAccountId);
    }

    [Fact]
    public async Task Record_SystemAction_UsesSystemActorEvenWhenLoggedIn()
    {
        using var f = new AuditFixture();
        f.LoginAs(Role.Administrator);
        f.Add(AuditAction.ApplicationStarted);

        var e = Assert.Single(await f.Repository.QueryAsync(All));
        Assert.Equal("system", e.ActorLogin);
        Assert.Null(e.ActorAccountId);
    }

    [Fact]
    public async Task Sanitiser_ReplacesCrLfAndControlCharacters()
    {
        using var f = new AuditFixture();
        f.Add(AuditAction.ReportGenerated, "a\r\nb", new Dictionary<string, string> { ["file"] = "x\r\n2026-01-01 FAKE;\tline" + (char)0x2028 + "y" });

        var e = Assert.Single(await f.Repository.QueryAsync(All));
        Assert.DoesNotContain('\r', e.Details + e.TargetId);
        Assert.DoesNotContain('\n', e.Details + e.TargetId);
        Assert.DoesNotContain('\t', e.Details);
        Assert.DoesNotContain((char)0x2028, e.Details);
        Assert.Equal("a  b", e.TargetId);
    }

    [Fact]
    public async Task Sanitiser_DropsForbiddenAndUnlistedKeys()
    {
        using var f = new AuditFixture();
        f.Add(AuditAction.LoginFailed, "bob", new Dictionary<string, string>
        {
            ["reason"] = "bad credentials",
            ["password"] = "hunter2",
            ["passwordHash"] = "abc",
            ["salt"] = "s",
            ["token"] = "t",
            ["clientSecret"] = "c",
            ["unlisted"] = "u",
        });

        var e = Assert.Single(await f.Repository.QueryAsync(All));
        Assert.Equal("reason=bad credentials", e.Details);
        Assert.False(AuditDetailsSanitizer.IsForbiddenKey("reason"));
        Assert.True(AuditDetailsSanitizer.IsForbiddenKey("passwordHash"));
    }

    [Fact]
    public void Sanitiser_LimitsDetailsTo2000Chars()
    {
        var text = AuditDetailsSanitizer.Sanitize(AuditAction.ReportGenerated, new Dictionary<string, string> { ["file"] = new string('a', 5000) });
        Assert.Equal(2000, text.Length);
    }

    [Fact]
    public void Record_UnknownAction_DoesNotThrowAndReportsToHook()
    {
        using var f = new AuditFixture();
        f.Recorder.Record("Nope.Unknown", AuditTarget.Audit, null, AuditOutcome.Success);
        Assert.Single(f.Errors);
    }

    [Fact]
    public async Task Update_IsRejectedByTrigger()
    {
        using var f = new AuditFixture();
        f.Add();

        var ex = Assert.Throws<SqliteException>(() => f.Raw("UPDATE AuditEvents SET Details = 'x'"));
        Assert.Contains("append-only", ex.Message);
        Assert.Single(await f.Repository.QueryAsync(All));
    }

    [Fact]
    public async Task RawDelete_IsRejectedByTrigger()
    {
        using var f = new AuditFixture();
        f.Add();

        Assert.Throws<SqliteException>(() => f.Raw("DELETE FROM AuditEvents"));
        Assert.Single(await f.Repository.QueryAsync(All));
    }

    [Fact]
    public void Repository_ExposesNoUpdateOrDeleteApiExceptRetention()
    {
        var names = typeof(IAuditRepository).GetMethods().Select(m => m.Name).ToList();
        Assert.DoesNotContain(names, n => n.Contains("Update") || n == "DeleteAsync" || n.Contains("Remove"));
        Assert.Contains("DeleteOlderThanAsync", names);
    }

    [Fact]
    public async Task ConcurrentWrites_KeepOneIntactChain()
    {
        using var f = new AuditFixture();
        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => f.Add())));

        Assert.Empty(f.Errors);
        var result = await f.Verifier.VerifyAsync();
        Assert.True(result.IsIntact);
        Assert.Equal(20, result.CheckedCount);
    }

    [Fact]
    public async Task Query_OrdersByTimeDescendingAndFilters()
    {
        using var f = new AuditFixture();
        f.LoginAs(Role.Administrator);
        f.Add(AuditAction.ReportGenerated);
        f.Add(AuditAction.LoginSucceeded);

        var page = await f.Query.QueryAsync(All);
        Assert.Equal(2, page.Total);
        Assert.Equal(AuditAction.LoginSucceeded, page.Items[0].ActionType);

        var filtered = await f.Query.QueryAsync(new AuditQuery(actionType: AuditAction.ReportGenerated));
        Assert.Single(filtered.Items);
    }

    [Fact]
    public async Task Query_ByOperator_IsDeniedAndAudited()
    {
        using var f = new AuditFixture();
        f.LoginAs(Role.Operator, "op");

        await Assert.ThrowsAsync<Reports.Core.Identity.Exceptions.AccessDeniedException>(() => f.Query.QueryAsync(All));
        var denied = Assert.Single(await f.Repository.QueryAsync(All));
        Assert.Equal(AuditAction.AccessDenied, denied.ActionType);
        Assert.Equal(AuditOutcome.Denied, denied.Outcome);
    }

    [Fact]
    public void AuditQuery_Validates()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AuditQuery(take: 1001));
        Assert.Throws<ArgumentException>(() => new AuditQuery(DateTime.UtcNow, DateTime.UtcNow.AddDays(-1)));
    }
}
