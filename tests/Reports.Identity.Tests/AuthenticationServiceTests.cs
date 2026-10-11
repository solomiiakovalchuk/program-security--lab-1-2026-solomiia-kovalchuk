using Reports.Core.Identity;
using Reports.Core.Identity.Abstractions;

namespace Reports.Identity.Tests;

/// <summary>
/// Tests for <see cref="AuthenticationService"/>: login, uniform failure, lockout and blocking.
/// </summary>
public sealed class AuthenticationServiceTests
{
    private const string Login = "operator1";
    private const string Password = "Correct#Horse1";

    private static readonly DateTime StartTime = new(2026, 10, 18, 9, 0, 0, DateTimeKind.Utc);

    private sealed class Harness
    {
        public Harness()
        {
            Repository = new InMemoryAccountRepository();
            Hasher = new PasswordHasher();
            Session = new TestSessionContext();
            Clock = new TestClock(StartTime);
            Service = new AuthenticationService(Repository, Hasher, Session, Clock.Now);
        }

        public InMemoryAccountRepository Repository { get; }

        public PasswordHasher Hasher { get; }

        public TestSessionContext Session { get; }

        public TestClock Clock { get; }

        public AuthenticationService Service { get; }
    }

    [Fact]
    public async Task Login_Succeeds_WithCorrectPassword_AndCreatesSession()
    {
        var harness = new Harness();
        var account = await TestAccounts.AddWithPasswordAsync(harness.Repository, harness.Hasher, Login, Password);

        var outcome = await harness.Service.LoginAsync("  operator1 ", Password);

        Assert.Equal(LoginOutcome.Success, outcome);
        Assert.NotNull(harness.Session.Current);
        Assert.Equal(account.Id, harness.Session.Current!.AccountId);
        Assert.Equal(Login, harness.Session.Current.Login);
        Assert.Equal(Role.Operator, harness.Session.Current.Role);
    }

    [Fact]
    public async Task Login_Fails_WithWrongPassword_AndIncrementsFailedAttempts()
    {
        var harness = new Harness();
        await TestAccounts.AddWithPasswordAsync(harness.Repository, harness.Hasher, Login, Password);

        var outcome = await harness.Service.LoginAsync(Login, "Wrong#Password9");

        Assert.Equal(LoginOutcome.Failed, outcome);
        Assert.Null(harness.Session.Current);
        var stored = await harness.Repository.FindByLoginAsync(Account.NormalizeLogin(Login));
        Assert.Equal(1, stored!.FailedAttempts);
    }

    [Fact]
    public async Task Login_LocksAccount_After5FailedAttempts()
    {
        var harness = new Harness();
        await TestAccounts.AddWithPasswordAsync(harness.Repository, harness.Hasher, Login, Password);

        for (var attempt = 1; attempt <= AuthenticationService.MaxFailedAttempts; attempt++)
        {
            Assert.Equal(LoginOutcome.Failed, await harness.Service.LoginAsync(Login, "Wrong#Password9"));
        }

        var stored = await harness.Repository.FindByLoginAsync(Account.NormalizeLogin(Login));
        Assert.NotNull(stored!.LockedUntilUtc);
        Assert.Equal(StartTime + AuthenticationService.LockoutDuration, stored.LockedUntilUtc);

        // The correct password is refused while the account is locked.
        Assert.Equal(LoginOutcome.Failed, await harness.Service.LoginAsync(Login, Password));
        Assert.Null(harness.Session.Current);

        // After the lockout period the correct password works again.
        harness.Clock.UtcNow = StartTime + AuthenticationService.LockoutDuration + TimeSpan.FromSeconds(1);
        Assert.Equal(LoginOutcome.Success, await harness.Service.LoginAsync(Login, Password));
        Assert.Equal(0, stored.FailedAttempts);
        Assert.Null(stored.LockedUntilUtc);
    }

    [Fact]
    public async Task Login_Fails_Uniformly_ForUnknownLogin()
    {
        var harness = new Harness();
        await TestAccounts.AddWithPasswordAsync(harness.Repository, harness.Hasher, Login, Password);

        var unknownLogin = await harness.Service.LoginAsync("nobody42", Password);
        var wrongPassword = await harness.Service.LoginAsync(Login, "Wrong#Password9");

        Assert.Equal(LoginOutcome.Failed, unknownLogin);
        Assert.Equal(wrongPassword, unknownLogin);
        Assert.Null(harness.Session.Current);
    }

    [Fact]
    public async Task Login_Fails_WhenAccountBlocked()
    {
        var harness = new Harness();
        var account = await TestAccounts.AddWithPasswordAsync(harness.Repository, harness.Hasher, Login, Password);
        account.Block();
        await harness.Repository.UpdateAsync(account);

        var outcome = await harness.Service.LoginAsync(Login, Password);

        Assert.Equal(LoginOutcome.Failed, outcome);
        Assert.Null(harness.Session.Current);
    }

    [Fact]
    public async Task Login_RequiresInitialPassword_ForAccountWithoutPassword()
    {
        var harness = new Harness();
        await new BootstrapService().EnsureBuiltInAdminAsync(harness.Repository);

        var outcome = await harness.Service.LoginAsync("ADMIN", "anything");

        Assert.Equal(LoginOutcome.RequiresInitialPassword, outcome);
        Assert.Null(harness.Session.Current);
    }

    [Fact]
    public async Task Login_RequiresPasswordChange_WhenMustChangePasswordIsSet()
    {
        var harness = new Harness();
        var account = await TestAccounts.AddWithPasswordAsync(harness.Repository, harness.Hasher, Login, Password);
        account.SetPasswordHash(harness.Hasher.HashPassword(Password), mustChangePassword: true);
        await harness.Repository.UpdateAsync(account);

        var outcome = await harness.Service.LoginAsync(Login, Password);

        Assert.Equal(LoginOutcome.RequiresPasswordChange, outcome);
        Assert.NotNull(harness.Session.Current);
    }
}
