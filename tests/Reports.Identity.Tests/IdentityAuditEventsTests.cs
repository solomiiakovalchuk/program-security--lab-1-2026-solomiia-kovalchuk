using Reports.Core.Audit;
using Reports.Core.Identity;
using Reports.Core.Identity.Abstractions;

namespace Reports.Identity.Tests;

/// <summary>
/// Tests that Identity services emit the documented audit events and never leak secrets into them.
/// </summary>
public sealed class IdentityAuditEventsTests
{
    private const string Login = "operator1";
    private const string Password = "Correct#Horse1";
    private const string WrongPassword = "Wr0ng#Secret";
    private const string UnknownLogin = "ghostlogin42";
    private const string NewPassword = "Brand#New456";
    private const string TempPassword = "Temp#Pass123";

    private static readonly DateTime StartTime = new(2026, 10, 18, 9, 0, 0, DateTimeKind.Utc);

    private sealed class Harness
    {
        public Harness()
        {
            Repository = new InMemoryAccountRepository();
            Hasher = new PasswordHasher();
            Session = new TestSessionContext();
            Sink = new CapturingAuditSink();
            Clock = new TestClock(StartTime);
            Auth = new AuthenticationService(Repository, Hasher, Session, Clock.Now, Sink);
        }

        public InMemoryAccountRepository Repository { get; }

        public PasswordHasher Hasher { get; }

        public TestSessionContext Session { get; }

        public CapturingAuditSink Sink { get; }

        public TestClock Clock { get; }

        public AuthenticationService Auth { get; }
    }

    [Fact]
    public async Task LoginSucceeded_IsRecorded_WithAccountId()
    {
        var h = new Harness();
        var account = await TestAccounts.AddWithPasswordAsync(h.Repository, h.Hasher, Login, Password);

        await h.Auth.LoginAsync(Login, Password);

        var e = Assert.Single(h.Sink.Events);
        Assert.Equal(AuditAction.LoginSucceeded, e.Action);
        Assert.Equal(AuditTarget.Account, e.Target);
        Assert.Equal(account.Id.ToString(), e.TargetId);
        Assert.Equal(AuditOutcome.Success, e.Outcome);
        AssertNoSecrets(h);
    }

    [Fact]
    public async Task LoginFailed_KnownLogin_IsRecordedWithLoginKnownTrue()
    {
        var h = new Harness();
        var account = await TestAccounts.AddWithPasswordAsync(h.Repository, h.Hasher, Login, Password);

        await h.Auth.LoginAsync(Login, WrongPassword);

        var e = Assert.Single(h.Sink.Events);
        Assert.Equal(AuditAction.LoginFailed, e.Action);
        Assert.Equal(AuditOutcome.Failure, e.Outcome);
        Assert.Equal(account.Id.ToString(), e.TargetId);
        Assert.Equal("true", e.Details["loginKnown"]);
        AssertNoSecrets(h);
    }

    [Fact]
    public async Task LoginFailed_UnknownLogin_RecordsNeitherLoginNorPassword()
    {
        var h = new Harness();

        await h.Auth.LoginAsync(UnknownLogin, WrongPassword);

        var e = Assert.Single(h.Sink.Events);
        Assert.Equal(AuditAction.LoginFailed, e.Action);
        Assert.Null(e.TargetId);
        Assert.Equal("false", e.Details["loginKnown"]);
        AssertNoSecrets(h);
    }

    [Fact]
    public async Task FifthFailure_RecordsAccountLockedOut()
    {
        var h = new Harness();
        var account = await TestAccounts.AddWithPasswordAsync(h.Repository, h.Hasher, Login, Password);

        for (var i = 0; i < AuthenticationService.MaxFailedAttempts; i++)
        {
            await h.Auth.LoginAsync(Login, WrongPassword);
        }

        Assert.Equal(AuthenticationService.MaxFailedAttempts, h.Sink.Events.Count(e => e.Action == AuditAction.LoginFailed));
        var locked = Assert.Single(h.Sink.Events, e => e.Action == AuditAction.AccountLockedOut);
        Assert.Equal(account.Id.ToString(), locked.TargetId);
        Assert.Equal(h.Sink.Events.Last(), locked);
        AssertNoSecrets(h);
    }

    [Fact]
    public async Task PasswordChanged_IsRecorded_ForInitialAndRegularChange()
    {
        var h = new Harness();
        await new BootstrapService().EnsureBuiltInAdminAsync(h.Repository);
        var account = await TestAccounts.AddWithPasswordAsync(h.Repository, h.Hasher, Login, Password);
        var service = new ChangePasswordService(h.Sink);

        await service.ChangePasswordAsync(h.Repository, h.Hasher, account, Password, NewPassword);

        var e = Assert.Single(h.Sink.Events);
        Assert.Equal(AuditAction.PasswordChanged, e.Action);
        Assert.Equal(account.Id.ToString(), e.TargetId);
        Assert.Equal("Self", e.Details["initiator"]);

        var admin = (await h.Repository.FindByLoginAsync("ADMIN"))!;
        await service.SetInitialPasswordAsync(h.Repository, h.Hasher, admin, NewPassword);
        Assert.Equal("Initial", h.Sink.Events.Last().Details["initiator"]);
        AssertNoSecrets(h);
    }

    [Fact]
    public async Task Logout_IsRecordedAndClearsSession_OnlyWhenLoggedIn()
    {
        var h = new Harness();
        var account = await TestAccounts.AddWithPasswordAsync(h.Repository, h.Hasher, Login, Password);
        var sessions = new SessionService(h.Session, h.Sink);

        sessions.Logout();
        Assert.Empty(h.Sink.Events);

        await h.Auth.LoginAsync(Login, Password);
        sessions.Logout();

        Assert.Null(h.Session.Current);
        var e = h.Sink.Events.Last();
        Assert.Equal(AuditAction.Logout, e.Action);
        Assert.Equal(account.Id.ToString(), e.TargetId);
    }

    [Fact]
    public async Task Logout_IsRecordedWhileTheSessionIsStillSet()
    {
        var h = new Harness();
        await TestAccounts.AddWithPasswordAsync(h.Repository, h.Hasher, Login, Password);
        Session? seenAtRecordTime = null;
        var probe = new SessionProbeSink(h.Session, s => seenAtRecordTime = s);
        var sessions = new SessionService(h.Session, probe);

        await h.Auth.LoginAsync(Login, Password);
        sessions.Logout();

        Assert.NotNull(seenAtRecordTime);
        Assert.Equal(Login, seenAtRecordTime!.Login);
        Assert.Null(h.Session.Current);
    }

    private sealed class SessionProbeSink : IAuditSink
    {
        private readonly ISessionContext _session;
        private readonly Action<Session?> _onRecord;

        public SessionProbeSink(ISessionContext session, Action<Session?> onRecord)
        {
            _session = session;
            _onRecord = onRecord;
        }

        public void Record(string action, AuditTarget target, string? targetId, AuditOutcome outcome,
            IReadOnlyDictionary<string, string>? details = null) => _onRecord(_session.Current);
    }

    [Fact]
    public async Task AccountAdminEvents_AreRecorded_WithoutSecrets()
    {
        var h = new Harness();
        var admin = await TestAccounts.AddWithPasswordAsync(h.Repository, h.Hasher, "boss", Password, Role.Administrator);
        h.Session.Current = new Session(admin.Id, admin.Login, admin.Role);
        var service = new AccountManagementService(h.Repository, h.Hasher, h.Session, new AccessPolicyService(), h.Sink);

        var created = await service.CreateAccountAsync("worker", Role.Operator, TempPassword);
        await service.BlockAccountAsync(created.Id);
        await service.UnblockAccountAsync(created.Id);
        await service.ChangeRoleAsync(created.Id, Role.Administrator);
        await service.ResetPasswordAsync(created.Id, NewPassword);
        await service.DeleteAccountAsync(created.Id);

        var actions = h.Sink.Events.Select(e => e.Action).ToArray();
        Assert.Equal(
            new[]
            {
                AuditAction.AccountCreated,
                AuditAction.AccountBlocked,
                AuditAction.AccountUnblocked,
                AuditAction.AccountRoleChanged,
                AuditAction.PasswordResetByAdmin,
                AuditAction.AccountDeleted
            },
            actions);
        Assert.All(h.Sink.Events, e =>
        {
            Assert.Equal(AuditTarget.Account, e.Target);
            Assert.Equal(created.Id.ToString(), e.TargetId);
            Assert.Equal(AuditOutcome.Success, e.Outcome);
            Assert.Equal(admin.Id.ToString(), e.Details["performerId"]);
        });
        AssertNoSecrets(h);
    }

    [Fact]
    public async Task AccessDenied_IsRecorded_ForOperatorAndNoSession()
    {
        var h = new Harness();
        var op = await TestAccounts.AddWithPasswordAsync(h.Repository, h.Hasher, Login, Password);
        var service = new AccountManagementService(h.Repository, h.Hasher, h.Session, new AccessPolicyService(), h.Sink);

        await Assert.ThrowsAsync<Reports.Core.Identity.Exceptions.AccessDeniedException>(() => service.ListAccountsAsync());
        h.Session.Current = new Session(op.Id, op.Login, op.Role);
        await Assert.ThrowsAsync<Reports.Core.Identity.Exceptions.AccessDeniedException>(() => service.ListAccountsAsync());

        Assert.Equal(2, h.Sink.Events.Count);
        Assert.All(h.Sink.Events, e =>
        {
            Assert.Equal(AuditAction.AccessDenied, e.Action);
            Assert.Equal(AuditOutcome.Denied, e.Outcome);
            Assert.Equal("ManageAccounts", e.Details["action"]);
        });
        Assert.Null(h.Sink.Events[0].TargetId);
        Assert.Equal(op.Id.ToString(), h.Sink.Events[1].TargetId);
    }

    private static void AssertNoSecrets(Harness h)
    {
        var secrets = new List<string> { Password, WrongPassword, UnknownLogin, NewPassword, TempPassword };
        foreach (var account in h.Repository.GetAllAsync().Result)
        {
            if (account.PasswordHash is { } hash)
            {
                secrets.Add(hash);
                // salt is the third '$' segment of pbkdf2-sha256$iter$salt$hash
                var parts = hash.Split('$');
                if (parts.Length == 4)
                {
                    secrets.Add(parts[2]);
                    secrets.Add(parts[3]);
                }
            }
        }

        foreach (var e in h.Sink.Events)
        {
            var text = string.Join('|', e.Details.Keys.Concat(e.Details.Values)) + "|" + e.TargetId;
            foreach (var secret in secrets)
            {
                Assert.DoesNotContain(secret, text, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
