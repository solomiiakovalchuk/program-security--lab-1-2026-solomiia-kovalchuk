using Reports.Core.Audit;
using Reports.Core.Identity;
using Reports.Core.Identity.Abstractions;
using Reports.Desktop.Infrastructure;
using Reports.Desktop.ViewModels;
using Reports.Identity;

namespace Reports.Desktop.Tests;

internal sealed class CapturingSink : IAuditSink
{
    public List<(string Action, AuditOutcome Outcome)> Events { get; } = new();

    public void Record(string action, AuditTarget target, string? targetId, AuditOutcome outcome,
        IReadOnlyDictionary<string, string>? details = null) => Events.Add((action, outcome));
}

public sealed class LoginViewModelTests
{
    private const string Good = "Str0ng!pass";

    private readonly InMemoryAccountRepository _accounts = new();
    private readonly PasswordHasher _hasher = new();
    private readonly SessionContext _session = new();
    private readonly CapturingSink _sink = new();
    private readonly LoginViewModel _vm;

    public LoginViewModelTests()
    {
        var auth = new AuthenticationService(_accounts, _hasher, _session, null, _sink);
        _vm = new LoginViewModel(auth, new ChangePasswordService(_sink), _accounts, _hasher);
    }

    private async Task AddAsync(string login, Role role, string password, bool mustChange = false, bool blocked = false)
    {
        var account = Account.Create(login, role, true);
        account.SetPasswordHash(_hasher.HashPassword(password), mustChange);
        if (blocked) account.Block();
        await _accounts.AddAsync(account);
    }

    private async Task SignInAsync(string login, string password)
    {
        _vm.Login = login;
        _vm.Password = password;
        await _vm.SubmitAsync();
    }

    [Fact]
    public async Task Success_sets_session_and_raises_LoggedIn()
    {
        await AddAsync("oper", Role.Operator, Good);
        var raised = 0;
        _vm.LoggedIn += (_, _) => raised++;

        await SignInAsync("OPER", Good);

        Assert.Equal(1, raised);
        Assert.Equal(string.Empty, _vm.Error);
        Assert.NotNull(_session.Current);
        Assert.Equal("oper", _session.Current!.Login);
        Assert.Equal(Role.Operator, _session.Current.Role);
    }

    [Fact]
    public async Task Failures_show_one_uniform_message_without_session()
    {
        await AddAsync("oper", Role.Operator, Good);
        await AddAsync("blocked", Role.Operator, Good, blocked: true);
        var messages = new List<string>();

        foreach (var (login, password) in new[] { ("nobody", Good), ("oper", "wrong"), ("blocked", Good) })
        {
            await SignInAsync(login, password);
            messages.Add(_vm.Error);
        }

        Assert.All(messages, m => Assert.Equal(UiText.LoginFailed, m));
        Assert.Null(_session.Current);
        Assert.Equal(LoginStep.Credentials, _vm.Step);
    }

    [Fact]
    public async Task Lockout_after_five_failures_stays_uniform()
    {
        await AddAsync("oper", Role.Operator, Good);
        var raised = 0;
        _vm.LoggedIn += (_, _) => raised++;

        for (var i = 0; i < AuthenticationService.MaxFailedAttempts; i++)
        {
            await SignInAsync("oper", "wrong" + i);
            Assert.Equal(UiText.LoginFailed, _vm.Error);
        }

        // Правильний пароль на заблокованому записі: та сама відмова.
        await SignInAsync("oper", Good);

        Assert.Equal(UiText.LoginFailed, _vm.Error);
        Assert.Equal(0, raised);
        Assert.Null(_session.Current);
        Assert.Contains(_sink.Events, e => e.Action == AuditAction.AccountLockedOut);
    }

    [Fact]
    public async Task Builtin_admin_without_password_goes_to_initial_password_step()
    {
        await new BootstrapService().EnsureBuiltInAdminAsync(_accounts);

        await SignInAsync("ADMIN", string.Empty);

        Assert.Equal(LoginStep.InitialPassword, _vm.Step);
        Assert.Null(_session.Current);
        Assert.Equal(string.Empty, _vm.Error);
    }

    [Fact]
    public async Task Initial_password_errors_are_shown_in_ukrainian_and_block_progress()
    {
        await new BootstrapService().EnsureBuiltInAdminAsync(_accounts);
        await SignInAsync("ADMIN", string.Empty);
        var raised = 0;
        _vm.LoggedIn += (_, _) => raised++;

        _vm.NewPassword = "short";
        _vm.ConfirmPassword = "short";
        await _vm.SubmitAsync();
        Assert.Contains("щонайменше 8 символів", _vm.Error);
        Assert.Contains("цифру", _vm.Error);
        Assert.DoesNotContain("Password must", _vm.Error);

        _vm.NewPassword = Good;
        _vm.ConfirmPassword = Good + "x";
        await _vm.SubmitAsync();
        Assert.Contains("не збігаються", _vm.Error);

        Assert.Equal(0, raised);
        Assert.Null(_session.Current);
        Assert.Equal(LoginStep.InitialPassword, _vm.Step);
    }

    [Fact]
    public async Task Initial_password_success_logs_the_admin_in()
    {
        await new BootstrapService().EnsureBuiltInAdminAsync(_accounts);
        await SignInAsync("ADMIN", string.Empty);
        var raised = 0;
        _vm.LoggedIn += (_, _) => raised++;

        _vm.NewPassword = Good;
        _vm.ConfirmPassword = Good;
        await _vm.SubmitAsync();

        Assert.Equal(1, raised);
        Assert.Equal(Role.Administrator, _session.Current!.Role);
        var stored = await _accounts.FindByLoginAsync("ADMIN");
        Assert.NotNull(stored!.PasswordHash);
        Assert.False(stored.MustChangePassword);
        Assert.Contains(_sink.Events, e => e.Action == AuditAction.PasswordChanged);
    }

    [Fact]
    public async Task Forced_change_step_opens_with_session_and_surfaces_policy_errors()
    {
        await AddAsync("oper", Role.Operator, "Temp0rary!", mustChange: true);
        var raised = 0;
        _vm.LoggedIn += (_, _) => raised++;

        await SignInAsync("oper", "Temp0rary!");

        Assert.Equal(LoginStep.ChangePassword, _vm.Step);
        Assert.NotNull(_session.Current);
        Assert.Equal(0, raised);

        _vm.NewPassword = "nodigits!!";
        _vm.ConfirmPassword = "nodigits!!";
        await _vm.SubmitAsync();
        Assert.Contains("цифру", _vm.Error);

        _vm.NewPassword = "Temp0rary!";
        _vm.ConfirmPassword = "Temp0rary!";
        await _vm.SubmitAsync();
        Assert.Contains("відрізнятися", _vm.Error);
        Assert.Equal(0, raised);
    }

    [Fact]
    public async Task Forced_change_success_proceeds_and_new_password_works()
    {
        await AddAsync("oper", Role.Operator, "Temp0rary!", mustChange: true);
        await SignInAsync("oper", "Temp0rary!");
        var raised = 0;
        _vm.LoggedIn += (_, _) => raised++;

        _vm.NewPassword = Good;
        _vm.ConfirmPassword = Good;
        await _vm.SubmitAsync();

        Assert.Equal(1, raised);
        Assert.Equal(string.Empty, _vm.Error);
        Assert.False((await _accounts.FindByLoginAsync("OPER"))!.MustChangePassword);

        _session.Current = null;
        _vm.Reset();
        await SignInAsync("oper", Good);
        Assert.NotNull(_session.Current);
    }

    [Fact]
    public async Task Back_from_forced_change_closes_the_session()
    {
        await AddAsync("oper", Role.Operator, "Temp0rary!", mustChange: true);
        await SignInAsync("oper", "Temp0rary!");

        await _vm.BackAsync();

        Assert.Null(_session.Current);
        Assert.Equal(LoginStep.Credentials, _vm.Step);
        Assert.Contains(_sink.Events, e => e.Action == AuditAction.Logout);
    }

    [Fact]
    public async Task Integrity_warning_flag_is_passed_through()
    {
        var auth = new AuthenticationService(_accounts, _hasher, _session, null, _sink);
        var vm = new LoginViewModel(auth, new ChangePasswordService(_sink), _accounts, _hasher, integrityWarning: true);
        Assert.True(vm.IntegrityWarning);
        Assert.Contains("не підписано", vm.IntegrityWarningText);
        await Task.CompletedTask;
    }
}

public sealed class ChangePasswordViewModelTests
{
    [Fact]
    public async Task Own_password_change_requires_session_and_reports_policy_errors()
    {
        var accounts = new InMemoryAccountRepository();
        var hasher = new PasswordHasher();
        var session = new SessionContext();
        var sink = new CapturingSink();
        var account = Account.Create("oper", Role.Operator, true);
        account.SetPasswordHash(hasher.HashPassword("Old0pass!"), false);
        await accounts.AddAsync(account);
        var guard = new AccessGuard(session, new AccessPolicyService(), sink);
        var vm = new ChangePasswordViewModel(new ChangePasswordService(sink), accounts, hasher, guard);

        // Без сесії: відмова в доступі показується текстом, а не винятком.
        vm.CurrentPassword = "Old0pass!";
        vm.NewPassword = "New0pass!";
        vm.ConfirmPassword = "New0pass!";
        await vm.SubmitAsync();
        Assert.Contains("прав", vm.Error);

        session.Current = new Session(account.Id, "oper", Role.Operator);
        vm.NewPassword = "weak";
        vm.ConfirmPassword = "weak";
        await vm.SubmitAsync();
        Assert.Contains("щонайменше 8", vm.Error);

        var changed = 0;
        vm.Changed += (_, _) => changed++;
        vm.NewPassword = "New0pass!";
        vm.ConfirmPassword = "New0pass!";
        await vm.SubmitAsync();
        Assert.Equal(1, changed);
        Assert.Equal(string.Empty, vm.Error);
    }
}
