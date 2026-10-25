using Reports.Core.Audit;
using Reports.Core.Identity;
using Reports.Core.Identity.Exceptions;

namespace Reports.Identity.Tests;

/// <summary>
/// Tests for <see cref="AccountManagementService"/>: FR-28 flows, protections and access checks.
/// </summary>
public sealed class AccountManagementTests
{
    private const string AdminPassword = "Admin#Pass1";
    private const string TempPassword = "Temp#Pass123";
    private const string NewPassword = "Brand#New456";

    private sealed class Harness
    {
        public Harness()
        {
            Repository = new InMemoryAccountRepository();
            Hasher = new PasswordHasher();
            Session = new TestSessionContext();
            Sink = new CapturingAuditSink();
            Service = new AccountManagementService(Repository, Hasher, Session, new AccessPolicyService(), Sink);
        }

        public InMemoryAccountRepository Repository { get; }

        public PasswordHasher Hasher { get; }

        public TestSessionContext Session { get; }

        public CapturingAuditSink Sink { get; }

        public AccountManagementService Service { get; }

        public async Task<Account> LoginAsAdminAsync(string login = "boss")
        {
            var admin = await TestAccounts.AddWithPasswordAsync(Repository, Hasher, login, AdminPassword, Role.Administrator);
            Session.Current = new Session(admin.Id, admin.Login, admin.Role);
            return admin;
        }
    }

    [Fact]
    public async Task Create_StoresAccountWithForcedPasswordChange()
    {
        var h = new Harness();
        await h.LoginAsAdminAsync();

        var info = await h.Service.CreateAccountAsync("  newuser ", Role.Operator, TempPassword);

        Assert.Equal("newuser", info.Login);
        Assert.True(info.MustChangePassword);
        Assert.True(info.HasPassword);
        var stored = await h.Repository.FindByLoginAsync("NEWUSER");
        Assert.NotNull(stored);
        Assert.True(h.Hasher.VerifyPassword(TempPassword, stored!.PasswordHash!));
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("bad login")]
    [InlineData("bad/login")]
    public async Task Create_RejectsInvalidLogin(string login)
    {
        var h = new Harness();
        await h.LoginAsAdminAsync();

        await Assert.ThrowsAsync<DomainRuleViolationException>(
            () => h.Service.CreateAccountAsync(login, Role.Operator, TempPassword));
    }

    [Fact]
    public async Task Create_RejectsWeakPasswordAndDuplicateLogin()
    {
        var h = new Harness();
        await h.LoginAsAdminAsync();

        await Assert.ThrowsAsync<DomainRuleViolationException>(
            () => h.Service.CreateAccountAsync("newuser", Role.Operator, "short"));
        await h.Service.CreateAccountAsync("newuser", Role.Operator, TempPassword);
        await Assert.ThrowsAsync<DomainRuleViolationException>(
            () => h.Service.CreateAccountAsync("NEWUSER", Role.Operator, TempPassword));
    }

    [Fact]
    public async Task BlockAndUnblock_ChangeBlockedFlag()
    {
        var h = new Harness();
        await h.LoginAsAdminAsync();
        var user = await h.Service.CreateAccountAsync("worker", Role.Operator, TempPassword);

        await h.Service.BlockAccountAsync(user.Id);
        Assert.True((await h.Service.ListAccountsAsync()).Single(a => a.Id == user.Id).IsBlocked);

        await h.Service.UnblockAccountAsync(user.Id);
        Assert.False((await h.Service.ListAccountsAsync()).Single(a => a.Id == user.Id).IsBlocked);
    }

    [Fact]
    public async Task Delete_RemovesAccount()
    {
        var h = new Harness();
        await h.LoginAsAdminAsync();
        var user = await h.Service.CreateAccountAsync("worker", Role.Operator, TempPassword);

        await h.Service.DeleteAccountAsync(user.Id);

        Assert.Null(await h.Repository.FindByLoginAsync("WORKER"));
    }

    [Fact]
    public async Task ChangeRole_UpdatesRole()
    {
        var h = new Harness();
        await h.LoginAsAdminAsync();
        var user = await h.Service.CreateAccountAsync("worker", Role.Operator, TempPassword);

        await h.Service.ChangeRoleAsync(user.Id, Role.Administrator);

        Assert.Equal(Role.Administrator, (await h.Service.ListAccountsAsync()).Single(a => a.Id == user.Id).Role);
    }

    [Fact]
    public async Task ResetPassword_SetsTemporaryPassword_ForcesChange_AndClearsLockout()
    {
        var h = new Harness();
        await h.LoginAsAdminAsync();
        var user = await h.Service.CreateAccountAsync("worker", Role.Operator, TempPassword);
        var stored = (await h.Repository.FindByLoginAsync("WORKER"))!;
        var auth = new AuthenticationService(h.Repository, h.Hasher, new TestSessionContext());
        for (var i = 0; i < AuthenticationService.MaxFailedAttempts; i++)
        {
            await auth.LoginAsync("worker", "wrong");
        }

        Assert.NotNull(stored.LockedUntilUtc);

        await h.Service.ResetPasswordAsync(user.Id, NewPassword);

        Assert.Null(stored.LockedUntilUtc);
        Assert.Equal(0, stored.FailedAttempts);
        Assert.True(stored.MustChangePassword);
        Assert.Equal(LoginOutcome.RequiresPasswordChange, await auth.LoginAsync("worker", NewPassword));
    }

    [Fact]
    public async Task ResetPassword_AppliesPasswordPolicy_AndRejectsSelf()
    {
        var h = new Harness();
        var admin = await h.LoginAsAdminAsync();
        var user = await h.Service.CreateAccountAsync("worker", Role.Operator, TempPassword);

        await Assert.ThrowsAsync<DomainRuleViolationException>(() => h.Service.ResetPasswordAsync(user.Id, "weak"));
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => h.Service.ResetPasswordAsync(admin.Id, NewPassword));
    }

    [Fact]
    public async Task LastActiveAdministrator_CannotBeDeletedBlockedOrDemoted()
    {
        var h = new Harness();
        var boss = await h.LoginAsAdminAsync();
        // The acting session is an administrator identity that is not in the store, so 'boss' is the only active admin.
        h.Session.Current = new Session(Guid.NewGuid(), "ghost", Role.Administrator);

        await Assert.ThrowsAsync<DomainRuleViolationException>(() => h.Service.DeleteAccountAsync(boss.Id));
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => h.Service.BlockAccountAsync(boss.Id));
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => h.Service.ChangeRoleAsync(boss.Id, Role.Operator));
        Assert.Equal(Role.Administrator, boss.Role);
        Assert.False(boss.IsBlocked);
    }

    [Fact]
    public async Task BlockedAdministrator_CanBeDeleted_WhenAnotherActiveAdminExists()
    {
        var h = new Harness();
        await h.LoginAsAdminAsync();
        var second = await h.Service.CreateAccountAsync("second", Role.Administrator, TempPassword);
        await h.Service.BlockAccountAsync(second.Id);

        await h.Service.DeleteAccountAsync(second.Id);

        Assert.Null(await h.Repository.FindByLoginAsync("SECOND"));
    }

    [Fact]
    public async Task BuiltInAdmin_CannotBeDeletedBlockedOrDemoted_ButPasswordCanBeReset()
    {
        var h = new Harness();
        await new BootstrapService().EnsureBuiltInAdminAsync(h.Repository);
        await h.LoginAsAdminAsync();
        var builtIn = (await h.Repository.FindByLoginAsync("ADMIN"))!;

        await Assert.ThrowsAsync<DomainRuleViolationException>(() => h.Service.DeleteAccountAsync(builtIn.Id));
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => h.Service.BlockAccountAsync(builtIn.Id));
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => h.Service.ChangeRoleAsync(builtIn.Id, Role.Operator));

        await h.Service.ResetPasswordAsync(builtIn.Id, NewPassword);
        Assert.True(builtIn.MustChangePassword);
        Assert.NotNull(builtIn.PasswordHash);
    }

    [Fact]
    public async Task SelfProtection_BlocksOwnDeleteBlockAndRoleChange()
    {
        var h = new Harness();
        var admin = await h.LoginAsAdminAsync();
        await h.Service.CreateAccountAsync("second", Role.Administrator, TempPassword);

        await Assert.ThrowsAsync<DomainRuleViolationException>(() => h.Service.DeleteAccountAsync(admin.Id));
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => h.Service.BlockAccountAsync(admin.Id));
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => h.Service.ChangeRoleAsync(admin.Id, Role.Operator));
    }

    [Fact]
    public async Task Operator_IsDeniedEveryMethod_AndDenialIsAudited()
    {
        var h = new Harness();
        var op = await TestAccounts.AddWithPasswordAsync(h.Repository, h.Hasher, "oper", AdminPassword);
        h.Session.Current = new Session(op.Id, op.Login, op.Role);

        await AssertAllDeniedAsync(h, op.Id);

        Assert.Equal(7, h.Sink.Events.Count(e => e.Action == AuditAction.AccessDenied && e.Outcome == AuditOutcome.Denied));
        Assert.Single(await h.Repository.GetAllAsync());
    }

    [Fact]
    public async Task NoSession_IsDeniedEveryMethod()
    {
        var h = new Harness();
        var op = await TestAccounts.AddWithPasswordAsync(h.Repository, h.Hasher, "oper", AdminPassword);

        await AssertAllDeniedAsync(h, op.Id);

        Assert.Equal(7, h.Sink.Events.Count(e => e.Action == AuditAction.AccessDenied));
    }

    [Fact]
    public async Task List_NeverExposesHashOrSalt()
    {
        var h = new Harness();
        await h.LoginAsAdminAsync();
        await h.Service.CreateAccountAsync("worker", Role.Operator, TempPassword);

        var list = await h.Service.ListAccountsAsync();

        Assert.Equal(2, list.Count);
        Assert.DoesNotContain(typeof(AccountInfo).GetProperties(), p => p.Name.Contains("Hash") || p.Name.Contains("Salt"));
        var text = string.Join('|', list.Select(a => a.ToString()));
        foreach (var account in await h.Repository.GetAllAsync())
        {
            Assert.DoesNotContain(account.PasswordHash!, text);
        }
    }

    private static async Task AssertAllDeniedAsync(Harness h, Guid id)
    {
        await Assert.ThrowsAsync<AccessDeniedException>(() => h.Service.CreateAccountAsync("newuser", Role.Operator, TempPassword));
        await Assert.ThrowsAsync<AccessDeniedException>(() => h.Service.BlockAccountAsync(id));
        await Assert.ThrowsAsync<AccessDeniedException>(() => h.Service.UnblockAccountAsync(id));
        await Assert.ThrowsAsync<AccessDeniedException>(() => h.Service.DeleteAccountAsync(id));
        await Assert.ThrowsAsync<AccessDeniedException>(() => h.Service.ChangeRoleAsync(id, Role.Administrator));
        await Assert.ThrowsAsync<AccessDeniedException>(() => h.Service.ResetPasswordAsync(id, NewPassword));
        await Assert.ThrowsAsync<AccessDeniedException>(() => h.Service.ListAccountsAsync());
    }
}
