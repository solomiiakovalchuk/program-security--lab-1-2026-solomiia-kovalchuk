using Reports.Core.Audit;
using Reports.Core.Identity;
using Reports.Core.Identity.Exceptions;

namespace Reports.Audit.Tests;

public class AuditChainTests
{
    private static AuditFixture Filled(int n)
    {
        var f = new AuditFixture();
        for (var i = 0; i < n; i++)
        {
            f.Add(targetId: "t" + i);
        }

        return f;
    }

    [Fact]
    public async Task IntactChain_IsOk()
    {
        using var f = Filled(5);
        var r = await f.Verifier.VerifyAsync();
        Assert.True(r.IsIntact);
        Assert.Equal(5, r.CheckedCount);
        Assert.Null(r.FirstBrokenId);
    }

    [Fact]
    public async Task EmptyLog_IsOk()
    {
        using var f = new AuditFixture();
        Assert.True((await f.Verifier.VerifyAsync()).IsIntact);
    }

    [Fact]
    public async Task ChangedContent_IsDetectedAtThatRecord()
    {
        using var f = Filled(5);
        f.Raw("DROP TRIGGER AuditEvents_NoUpdate");
        f.Raw("UPDATE AuditEvents SET TargetId = 'forged' WHERE Id = 3");

        var r = await f.Verifier.VerifyAsync();
        Assert.False(r.IsIntact);
        Assert.Equal(3, r.FirstBrokenId);
        Assert.Equal(2, r.FirstBrokenIndex);
    }

    [Fact]
    public async Task DeletedMiddleRecord_IsDetected()
    {
        using var f = Filled(5);
        f.Raw("DROP TRIGGER AuditEvents_RetentionOnlyDelete");
        f.Raw("DELETE FROM AuditEvents WHERE Id = 3");

        var r = await f.Verifier.VerifyAsync();
        Assert.False(r.IsIntact);
        Assert.Equal(4, r.FirstBrokenId);
    }

    [Fact]
    public async Task ReorderedRecords_AreDetected()
    {
        using var f = Filled(5);
        f.Raw("DROP TRIGGER AuditEvents_NoUpdate");
        f.Raw("UPDATE AuditEvents SET Id = 100 WHERE Id = 2");
        f.Raw("UPDATE AuditEvents SET Id = 2 WHERE Id = 3");
        f.Raw("UPDATE AuditEvents SET Id = 3 WHERE Id = 100");

        var r = await f.Verifier.VerifyAsync();
        Assert.False(r.IsIntact);
        Assert.Equal(2, r.FirstBrokenId);
    }

    [Fact]
    public async Task Retention_AfterExport_KeepsChainValid_AndWithoutExportIsRefused()
    {
        using var f = new AuditFixture();
        f.LoginAs(Role.Administrator);
        f.Clock.UtcNow = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        f.Add();
        f.Add();
        f.Add();
        f.Clock.UtcNow = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        f.Add();

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Retention.ApplyAsync());

        await f.Export.ExportAsync(new AuditQuery(), AuditExportFormat.Json, new MemoryStream());
        var deleted = await f.Retention.ApplyAsync();

        Assert.Equal(3, deleted);
        Assert.True((await f.Verifier.VerifyAsync()).IsIntact);
        var left = await f.Repository.QueryAsync(new AuditQuery());
        Assert.Contains(left, e => e.ActionType == AuditAction.AuditRetentionApplied);
        Assert.DoesNotContain(left, e => e.OccurredAtUtc.Year == 2025);
    }

    [Fact]
    public void Retention_BelowMinimum_IsRejected()
    {
        using var f = new AuditFixture();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AuditRetentionService(f.Repository, f.Session, f.Policy, f.Recorder, f.Clock, 30));
    }

    [Fact]
    public async Task Retention_ByOperator_IsDenied()
    {
        using var f = Filled(1);
        f.LoginAs(Role.Operator, "op");
        await Assert.ThrowsAsync<AccessDeniedException>(() => f.Retention.ApplyAsync());
    }
}
