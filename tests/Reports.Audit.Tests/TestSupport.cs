using Microsoft.Data.Sqlite;
using Reports.Audit;
using Reports.Core.Audit;
using Reports.Core.Identity;
using Reports.Core.Identity.Abstractions;
using Reports.Data;
using Reports.Data.Repositories;
using Reports.Identity;

namespace Reports.Audit.Tests;

internal sealed class FakeSession : ISessionContext
{
    public Session? Current { get; set; }
}

internal sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
}

/// <summary>Migrated temp-file database with the audit services wired over it.</summary>
internal sealed class AuditFixture : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "audit-tests-" + Guid.NewGuid().ToString("N"));

    public AuditFixture()
    {
        Directory.CreateDirectory(_folder);
        DbPath = Path.Combine(_folder, "test.db");
        Factory = new SqliteContextFactory(DbPath);
        DatabaseInitializer.InitializeAsync(Factory).GetAwaiter().GetResult();

        Repository = new AuditRepository(Factory);
        Session = new FakeSession();
        Clock = new FakeClock();
        Policy = new AccessPolicyService();
        Recorder = new AuditRecordingService(Repository, Session, Clock, ex => Errors.Add(ex), "tester", "TESTPC");
        Verifier = new AuditChainVerifier(Repository);
        Query = new AuditQueryService(Repository, Session, Policy, Verifier, Recorder);
        Export = new AuditExportService(Repository, Session, Policy, Recorder, Clock);
        Retention = new AuditRetentionService(Repository, Session, Policy, Recorder, Clock);
    }

    public string DbPath { get; }
    public SqliteContextFactory Factory { get; }
    public AuditRepository Repository { get; }
    public FakeSession Session { get; }
    public FakeClock Clock { get; }
    public AccessPolicyService Policy { get; }
    public List<Exception> Errors { get; } = new();
    public AuditRecordingService Recorder { get; }
    public AuditChainVerifier Verifier { get; }
    public AuditQueryService Query { get; }
    public AuditExportService Export { get; }
    public AuditRetentionService Retention { get; }

    public void LoginAs(Role role, string login = "boss") =>
        Session.Current = new Session(Guid.NewGuid(), login, role);

    public void Add(string action = AuditAction.LoginSucceeded, string? targetId = "x", IReadOnlyDictionary<string, string>? details = null)
    {
        Clock.UtcNow = Clock.UtcNow.AddMinutes(1);
        Recorder.Record(action, AuditTarget.Account, targetId, AuditOutcome.Success, details);
    }

    public void Raw(string sql)
    {
        using var c = new SqliteConnection($"Data Source={DbPath}");
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_folder, true); } catch (IOException) { }
    }
}
